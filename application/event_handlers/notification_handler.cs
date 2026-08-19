// application/event_handlers/notification_handler.cs
using dnd_game.Domain.Events;
using Microsoft.Extensions.Logging;

namespace dnd_game.Application.EventHandlers;

/// <summary>
/// Заглушка обработчика уведомлений. Логирует события, которые требуют оповещения игроков.
/// В будущем может быть расширен отправкой push-уведомлений, email и т.д.
/// </summary>
public class NotificationHandler : IEventHandler<CharacterDied>,
                                   IEventHandler<CombatStarted>,
                                   IEventHandler<CharacterHealed>,
                                   IEventHandler<ConditionApplied>,
                                   IEventHandler<ConditionRemoved>,
                                   IEventHandler<SpellCast>
{
    private readonly ILogger<NotificationHandler> _logger;

    public NotificationHandler(ILogger<NotificationHandler> logger) => _logger = logger;

    public Task Handle(CharacterDied e, CancellationToken ct)
    {
        _logger.LogWarning("Notification: Character {Id} has died", e.CharacterId);
        return Task.CompletedTask;
    }

    public Task Handle(CombatStarted e, CancellationToken ct)
    {
        _logger.LogInformation("Notification: Combat {CombatId} started", e.CombatId);
        return Task.CompletedTask;
    }

    public Task Handle(CharacterHealed e, CancellationToken ct)
    {
        _logger.LogInformation("Notification: Character {Id} healed for {Amount}", e.CharacterId, e.Amount);
        return Task.CompletedTask;
    }

    public Task Handle(ConditionApplied e, CancellationToken ct)
    {
        _logger.LogInformation("Notification: Character {Id} gained condition {Condition}", e.CharacterId, e.Condition);
        return Task.CompletedTask;
    }

    public Task Handle(ConditionRemoved e, CancellationToken ct)
    {
        _logger.LogInformation("Notification: Character {Id} lost condition {Condition}", e.CharacterId, e.Condition);
        return Task.CompletedTask;
    }

    public Task Handle(SpellCast e, CancellationToken ct)
    {
        _logger.LogInformation("Notification: {CasterId} cast {SpellId} (target: {TargetId})", e.CasterId, e.SpellId, e.TargetId);
        return Task.CompletedTask;
    }
}