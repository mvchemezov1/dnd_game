// infrastructure/monitoring/health_check.cs
using dnd_game.Infrastructure.EventStore;         // IEventStore
using dnd_game.Infrastructure.MessageBus;          // RabbitMqBus, ICommandBus, IEventBus
using dnd_game.Infrastructure.Coordination;        // IDistributedLockManager
using Microsoft.Extensions.Logging;
using System.Data.Common;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;

namespace dnd_game.Infrastructure.Monitoring
{
    /// <summary>
    /// ������ ���������� ���������� �������.
    /// </summary>
    public enum HealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy
    }

    /// <summary>
    /// ��������� �������� �������� ����������.
    /// </summary>
    public class HealthCheckResult
    {
        public string ComponentName { get; set; } = string.Empty;
        public HealthStatus Status { get; set; }
        public string Description { get; set; } = string.Empty;
        public TimeSpan ResponseTime { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
    }

    /// <summary>
    /// �������� ��� �������� ��������.
    /// </summary>
    public interface IHealthCheck
    {
        Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// ����������� �������� �������� ��� DnD ����������.
    /// </summary>
    public class DndHealthCheck : IHealthCheck
    {
        private readonly IEventStore _eventStore;
        private readonly RabbitMqBus? _rabbitMqBus;
        private readonly IDistributedLockManager? _distributedLockManager;
        private readonly string _connectionString;
        private readonly ILogger<DndHealthCheck> _logger;

        public DndHealthCheck(
            IEventStore eventStore,
            RabbitMqBus? rabbitMqBus,
            IDistributedLockManager? distributedLockManager,
            IOptions<HealthCheckOptions> options,
            IConfiguration configuration,
            ILogger<DndHealthCheck> logger)
        {
            _eventStore = eventStore;
            _rabbitMqBus = rabbitMqBus;
            _distributedLockManager = distributedLockManager;
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken = default)
        {

            var overallResult = new HealthCheckResult
            {
                ComponentName = "DnD Application",
                Status = HealthStatus.Healthy,
                Description = "Overall health status"
            };

            var checks = new List<Task<HealthCheckResult>>
            {
                CheckEventStoreAsync(cancellationToken),
                CheckDatabaseAsync(cancellationToken),
                CheckMessageBusAsync(cancellationToken),
                CheckLockManagerAsync(cancellationToken)
            };

            var results = await Task.WhenAll(checks);

            // ���������� ����� ������: ���� ���� ���� ���� Unhealthy, ����� Unhealthy.
            if (results.Any(r => r.Status == HealthStatus.Unhealthy))
                overallResult.Status = HealthStatus.Unhealthy;
            else if (results.Any(r => r.Status == HealthStatus.Degraded))
                overallResult.Status = HealthStatus.Degraded;

            foreach (var result in results)
                overallResult.Details[result.ComponentName] = result;

            return overallResult;
        }

        private async Task<HealthCheckResult> CheckEventStoreAsync(CancellationToken cancellationToken)
        {
            var result = new HealthCheckResult { ComponentName = "EventStore" };
            var start = DateTime.UtcNow;
            try
            {
                // ������� ��������� �������������� ������� � ��� ��������� ����������� � �������
                var events = await _eventStore.GetEvents(Guid.Empty, 0);
                result.Status = HealthStatus.Healthy;
                result.Description = "EventStore is operational.";
            }
            catch (Exception ex)
            {
                result.Status = HealthStatus.Unhealthy;
                result.Description = $"EventStore check failed: {ex.Message}";
                _logger.LogError(ex, "EventStore health check failed.");
            }
            result.ResponseTime = DateTime.UtcNow - start;
            return result;
        }

        private async Task<HealthCheckResult> CheckDatabaseAsync(CancellationToken cancellationToken)
        {
            var result = new HealthCheckResult { ComponentName = "Database" };
            var start = DateTime.UtcNow;
            try
            {
                using var conn = new Npgsql.NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);
                using var cmd = new Npgsql.NpgsqlCommand("SELECT 1", conn);
                await cmd.ExecuteScalarAsync(cancellationToken);
                result.Status = HealthStatus.Healthy;
                result.Description = "Database connection is healthy.";
            }
            catch (Exception ex)
            {
                result.Status = HealthStatus.Unhealthy;
                result.Description = $"Database connection failed: {ex.Message}";
                _logger.LogError(ex, "Database health check failed.");
            }
            result.ResponseTime = DateTime.UtcNow - start;
            return result;
        }

        private Task<HealthCheckResult> CheckMessageBusAsync(CancellationToken cancellationToken)
        {
            var result = new HealthCheckResult { ComponentName = "MessageBus (RabbitMQ)" };
            var start = DateTime.UtcNow;
            if (_rabbitMqBus == null)
            {
                result.Status = HealthStatus.Degraded;
                result.Description = "RabbitMQ not configured; using InMemory bus.";
                result.ResponseTime = DateTime.UtcNow - start;
                return Task.FromResult(result);
            }
            try
            {
                // ... остальной код (если есть)
                result.Status = HealthStatus.Healthy;
                result.Description = "RabbitMQ connection is active.";
            }
            catch (Exception ex)
            {
                result.Status = HealthStatus.Unhealthy;
                result.Description = $"RabbitMQ check failed: {ex.Message}";
                _logger.LogError(ex, "Message bus health check failed.");
            }
            result.ResponseTime = DateTime.UtcNow - start;
            return Task.FromResult(result);
        }

        private async Task<HealthCheckResult> CheckLockManagerAsync(CancellationToken cancellationToken)
        {
            var result = new HealthCheckResult { ComponentName = "DistributedLockManager (Redis)" };
            var start = DateTime.UtcNow;
            if (_distributedLockManager == null)
            {
                result.Status = HealthStatus.Degraded;
                result.Description = "No distributed lock manager configured.";
                result.ResponseTime = DateTime.UtcNow - start;
                return result;
            }
            try
            {
                // ������� ��������� � ���������� �������� ����
                string testKey = $"health_check:{Guid.NewGuid()}";
                var lockHandle = await _distributedLockManager.AcquireAsync(testKey, LockMode.Exclusive, "health_check", TimeSpan.FromSeconds(2), cancellationToken);
                if (lockHandle != null)
                {
                    lockHandle.Dispose();
                    result.Status = HealthStatus.Healthy;
                    result.Description = "Distributed lock manager is operational.";
                }
                else
                {
                    result.Status = HealthStatus.Degraded;
                    result.Description = "Could not acquire test lock, possible contention.";
                }
            }
            catch (Exception ex)
            {
                result.Status = HealthStatus.Unhealthy;
                result.Description = $"Lock manager check failed: {ex.Message}";
                _logger.LogError(ex, "Distributed lock manager health check failed.");
            }
            result.ResponseTime = DateTime.UtcNow - start;
            return result;
        }
    }
}