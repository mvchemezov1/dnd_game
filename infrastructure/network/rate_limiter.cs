using System.Collections.Concurrent;
using dnd_game.Domain.Commands;
using dnd_game.Infrastructure.MessageBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace dnd_game.Infrastructure.Network
{
    // =====================================================================
    // Конфигурация и политики (добавлено в этот файл)
    // =====================================================================

    public enum RateLimitAlgorithm
    {
        TokenBucket,
        SlidingWindow
    }

    public class RateLimitPolicy
    {
        public string Name { get; set; } = string.Empty;
        public int MaxRequests { get; set; } = 30;
        public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(10);
        public RateLimitAlgorithm Algorithm { get; set; } = RateLimitAlgorithm.TokenBucket;
    }

    public class RateLimitConfiguration
    {
        public bool Enabled { get; set; } = true;
        public Dictionary<string, RateLimitPolicy> Policies { get; set; } = new();
        public int TokenBucketRefillAmount { get; set; } = 1;
    }

    // =====================================================================
    // Интерфейс и реализация
    // =====================================================================

    public interface IRateLimiter
    {
        bool IsAllowed(Guid clientId, string? policyName = null);
        Task<bool> TryConsumeAsync(Guid clientId, string policyName, CancellationToken cancellationToken = default);
        int GetRemainingAllowance(Guid clientId, string policyName);
    }

    public class RateLimiter : IRateLimiter
    {
        private readonly RateLimitConfiguration _config;
        private readonly ILogger<RateLimiter> _logger;
        private readonly ConcurrentDictionary<string, TokenBucket> _buckets = new();
        private readonly ConcurrentDictionary<string, SlidingWindow> _windows = new();

        public RateLimiter(IOptions<RateLimitConfiguration> config, ILogger<RateLimiter> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        public bool IsAllowed(Guid clientId, string? policyName = null)
        {
            if (!_config.Enabled) return true;
            string effectivePolicy = policyName ?? "global";
            var policy = GetPolicy(effectivePolicy);
            if (policy is null) return true;

            string bucketKey = $"{clientId}:{effectivePolicy}";

            if (policy.Algorithm == RateLimitAlgorithm.TokenBucket)
            {
                var bucket = _buckets.GetOrAdd(bucketKey, _ => new TokenBucket(policy.MaxRequests, policy.MaxRequests, policy.Window, _config.TokenBucketRefillAmount));
                return bucket.Consume();
            }
            else // SlidingWindow
            {
                var window = _windows.GetOrAdd(bucketKey, _ => new SlidingWindow(policy.MaxRequests, policy.Window));
                return window.Consume();
            }
        }

        public Task<bool> TryConsumeAsync(Guid clientId, string policyName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(IsAllowed(clientId, policyName));
        }

        public int GetRemainingAllowance(Guid clientId, string policyName)
        {
            string bucketKey = $"{clientId}:{policyName}";
            var policy = GetPolicy(policyName);
            if (policy is null) return int.MaxValue;

            if (policy.Algorithm == RateLimitAlgorithm.TokenBucket)
            {
                if (_buckets.TryGetValue(bucketKey, out var bucket))
                    return bucket.CurrentTokens;
                return policy.MaxRequests;
            }
            else
            {
                if (_windows.TryGetValue(bucketKey, out var window))
                    return window.Remaining;
                return policy.MaxRequests;
            }
        }

        private RateLimitPolicy? GetPolicy(string name)
        {
            _config.Policies.TryGetValue(name, out var policy);
            return policy;
        }

        // ---------- TokenBucket реализация ----------
        private class TokenBucket
        {
            private readonly int _maxTokens;
            private readonly TimeSpan _refillInterval;
            private readonly int _refillAmount;
            private int _currentTokens;
            private DateTime _lastRefillUtc;

            public int CurrentTokens => _currentTokens;

            public TokenBucket(int maxTokens, int initialTokens, TimeSpan refillInterval, int refillAmount)
            {
                _maxTokens = maxTokens;
                _currentTokens = initialTokens;
                _refillInterval = refillInterval;
                _refillAmount = refillAmount;
                _lastRefillUtc = DateTime.UtcNow;
            }

            public bool Consume()
            {
                Refill();
                if (_currentTokens > 0)
                {
                    _currentTokens--;
                    return true;
                }
                return false;
            }

            private void Refill()
            {
                var now = DateTime.UtcNow;
                if (now < _lastRefillUtc + _refillInterval) return;
                int intervals = (int)((now - _lastRefillUtc).Ticks / _refillInterval.Ticks);
                if (intervals <= 0) return;
                _currentTokens = Math.Min(_maxTokens, _currentTokens + intervals * _refillAmount);
                _lastRefillUtc = _lastRefillUtc.Add(TimeSpan.FromTicks(intervals * _refillInterval.Ticks));
            }
        }

        // ---------- SlidingWindow реализация ----------
        private class SlidingWindow
        {
            private readonly int _maxRequests;
            private readonly TimeSpan _window;
            private readonly Queue<DateTime> _timestamps = new();

            public int Remaining => Math.Max(0, _maxRequests - _timestamps.Count);

            public SlidingWindow(int maxRequests, TimeSpan window)
            {
                _maxRequests = maxRequests;
                _window = window;
            }

            public bool Consume()
            {
                var now = DateTime.UtcNow;
                while (_timestamps.Count > 0 && now - _timestamps.Peek() > _window)
                    _timestamps.Dequeue();

                if (_timestamps.Count < _maxRequests)
                {
                    _timestamps.Enqueue(now);
                    return true;
                }
                return false;
            }
        }
    }
}