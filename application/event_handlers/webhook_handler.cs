// application/event_handlers/webhook_handler.cs
using dnd_game.application.event_handlers;
using dnd_game.Domain.Events;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace dnd_game.Application.EventHandlers
{
    /// <summary>
    /// ������������ ������ webhook-����������.
    /// </summary>
    public class WebhookSubscription
    {
        public Guid Id { get; set; }
        public string EventType { get; set; } = string.Empty; // ��������, "CharacterDied", "CombatStarted", "*"
        public string Url { get; set; } = string.Empty;
        public string? Secret { get; set; } // ��� HMAC-�������
        public int MaxRetries { get; set; } = 3;
        public int TimeoutSeconds { get; set; } = 10;
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// ��������� �������� (����� ���� � ��, ���������������� �����).
    /// </summary>
    public interface IWebhookSubscriptionRepository
    {
        Task<IEnumerable<WebhookSubscription>> GetSubscriptionsForEventAsync(string eventType);
        Task<IEnumerable<WebhookSubscription>> GetAllAsync();
    }

    /// <summary>
    /// ������ �������� HTTP-�������� � ��������.
    /// </summary>
    public interface IWebhookClient
    {
        Task SendAsync(WebhookSubscription subscription, object payload, CancellationToken cancellationToken);
    }

    /// <summary>
    /// ���������� IWebhookClient � �������������� HttpClient.
    /// </summary>
    public class DefaultWebhookClient : IWebhookClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DefaultWebhookClient> _logger;

        public DefaultWebhookClient(HttpClient httpClient, ILogger<DefaultWebhookClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task SendAsync(WebhookSubscription subscription, object payload, CancellationToken cancellationToken)
        {
            string jsonPayload = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url)
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };

            // ��������� ��������� � ��������, ���� ����� ������
            if (!string.IsNullOrEmpty(subscription.Secret))
            {
                string signature = ComputeHmacSignature(jsonPayload, subscription.Secret);
                request.Headers.Add("X-DnD-Signature", signature);
            }

            // ��������� ������� � ���������������� ���������
            int attempt = 0;
            int maxRetries = subscription.MaxRetries;
            while (true)
            {
                attempt++;
                try
                {
                    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(subscription.TimeoutSeconds));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
                    var response = await _httpClient.SendAsync(request, linkedCts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogDebug("Webhook to {Url} succeeded on attempt {Attempt}", subscription.Url, attempt);
                        return;
                    }
                    _logger.LogWarning("Webhook to {Url} returned {StatusCode} on attempt {Attempt}",
                        subscription.Url, (int)response.StatusCode, attempt);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Webhook to {Url} failed on attempt {Attempt}", subscription.Url, attempt);
                }

                if (attempt >= maxRetries)
                {
                    _logger.LogError("Webhook to {Url} failed after {MaxRetries} attempts", subscription.Url, maxRetries);
                    return;
                }

                // ���������������� �������� ����� ��������� ��������
                int delayMs = (int)Math.Pow(2, attempt) * 1000;
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        private static string ComputeHmacSignature(string payload, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToBase64String(hash);
        }
    }

    /// <summary>
    /// �������� ���������� ��������.
    /// </summary>
    public class WebhookHandler : IEventHandler<IDomainEvent>
    {
        private readonly IWebhookSubscriptionRepository _subscriptionRepo;
        private readonly IWebhookClient _webhookClient;
        private readonly ILogger<WebhookHandler> _logger;

        public WebhookHandler(
            IWebhookSubscriptionRepository subscriptionRepo,
            IWebhookClient webhookClient,
            ILogger<WebhookHandler> logger)
        {
            _subscriptionRepo = subscriptionRepo;
            _webhookClient = webhookClient;
            _logger = logger;
        }

        public async Task Handle(IDomainEvent @event, CancellationToken cancellationToken)
        {
            string eventType = @event.GetType().Name;
            var subscriptions = await _subscriptionRepo.GetSubscriptionsForEventAsync(eventType);
            // ����� ��������� �������� � �������� "*" (��� �������)
            var wildcardSubscriptions = await _subscriptionRepo.GetSubscriptionsForEventAsync("*");
            var allSubscriptions = subscriptions.Concat(wildcardSubscriptions);

            foreach (var sub in allSubscriptions.Where(s => s.IsActive))
            {
                // ��������� ������ �������� ��������, �������������� � �������� �����������
                var payload = BuildPayload(@event);
                _logger.LogDebug("Sending webhook for event {EventType} to {Url}", eventType, sub.Url);

                // ���������� ����������, �� ������ ���������� ������ (fire-and-forget � ������������)
                _ = _webhookClient.SendAsync(sub, payload, cancellationToken);
            }
        }

        // ����������� �������� ������� � ������, ������� ��� ������� ��������.
        private object BuildPayload(IDomainEvent @event)
        {
            // ����� ��������� ������� � ��������� ������ ��� ������������ ���������.
            // ����� ��� ������� ���������� ��������� ���, �� � �������� ���� ����� ������� ��� ������ ���.
            var result = new Dictionary<string, object?>
            {
                ["eventType"] = @event.GetType().Name,
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["data"] = @event
            };
            // ���������� �������������� ����� ��� ���������� �������:
            if (@event is Domain.Events.ICharacterEvent charEvent)
            {
                result["characterId"] = charEvent.CharacterId;
            }
            return result;
        }
    }
}