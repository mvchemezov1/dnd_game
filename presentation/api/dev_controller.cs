using dnd_game.Application.EventHandlers;
using dnd_game.Infrastructure.AI;
using dnd_game.Infrastructure.EventStore;
using dnd_game.Infrastructure.Monitoring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dnd_game.Presentation.Api
{
    /// <summary>
    /// Диагностические эндпоинты для панели разработчика. Раньше их не было вообще —
    /// health-check/replay/webhooks/scripts существовали только как внутренние сервисы,
    /// без HTTP-доступа. Доступ только роли Admin.
    /// </summary>
    [ApiController]
    [Route("api/dev")]
    [Authorize(Policy = "RequireAdmin")]
    public class DevController(
        IHealthCheck healthCheck,
        IScriptRepository scriptRepository,
        IWebhookSubscriptionRepository webhookRepository,
        IReplayEventStore replayEventStore) : ControllerBase
    {
        /// <summary>Состояние БД, EventStore, шины сообщений и распределённых блокировок.</summary>
        [HttpGet("health")]
        public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
        {
            var result = await healthCheck.CheckAsync(cancellationToken);
            return Ok(result);
        }

        /// <summary>Список зарегистрированных AI-скриптов (ScriptEngine).</summary>
        [HttpGet("scripts")]
        public IActionResult GetScripts()
        {
            return Ok(scriptRepository.GetAllScriptNames());
        }

        /// <summary>Список зарегистрированных webhook-подписок.</summary>
        [HttpGet("webhooks")]
        public async Task<IActionResult> GetWebhooks()
        {
            var subscriptions = await webhookRepository.GetAllAsync();
            return Ok(subscriptions);
        }

        /// <summary>Реплей событий конкретного агрегата (для отладки).</summary>
        [HttpGet("replay/{aggregateId}")]
        public async Task<IActionResult> GetReplay(Guid aggregateId)
        {
            var events = await replayEventStore.GetEventsAsync(aggregateId);
            var count = await replayEventStore.GetEventCountAsync(aggregateId);
            return Ok(new { aggregateId, eventCount = count, events });
        }
    }
}