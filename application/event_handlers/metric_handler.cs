// application/event_handlers/metric_handler.cs
using dnd_game.Domain.Events;
using Microsoft.Extensions.Logging;

namespace dnd_game.Application.EventHandlers;

/// <summary>
/// Заглушка сборщика метрик. Логирует ключевые события.
/// В будущем может быть расширен отправкой в Prometheus/StatsD.
/// </summary>
public class MetricHandler : IEventHandler<CharacterCreated>,
                             IEventHandler<CharacterDamageTaken>,
                             IEventHandler<CharacterHealed>,
                             IEventHandler<CharacterDied>,
                             IEventHandler<CombatStarted>,
                             IEventHandler<CombatEnded>,
                             IEventHandler<SpellCast>,
                             IEventHandler<ExperienceGained>,
                             IEventHandler<RestStarted>,
                             IEventHandler<RestCompleted>,
                             IEventHandler<ConditionApplied>,
                             IEventHandler<ConditionRemoved>
{
    private readonly ILogger<MetricHandler> _logger;

    public MetricHandler(ILogger<MetricHandler> logger) => _logger = logger;

    public Task Handle(CharacterCreated e, CancellationToken ct)
    {
        _logger.LogInformation("Metric: Character created {Name} ({Id})", e.Name, e.CharacterId);
        return Task.CompletedTask;
    }

    public Task Handle(CharacterDamageTaken e, CancellationToken ct)
    {
        _logger.LogInformation("Metric: Damage dealt to {Id}: {Amount}", e.CharacterId, e.Amount);
        return Task.CompletedTask;
    }

    public Task Handle(CharacterHealed e, CancellationToken ct)
    {
        _logger.LogInformation("Metric: Healing to {Id}: {Amount}", e.CharacterId, e.Amount);
        return Task.CompletedTask;
    }

    public Task Handle(CharacterDied e, CancellationToken ct)
    {
        _logger.LogWarning("Metric: Character {Id} died", e.CharacterId);
        return Task.CompletedTask;
    }

    public Task Handle(CombatStarted e, CancellationToken ct)
    {
        _logger.LogInformation("Metric: Combat {CombatId} started with {Count} participants", e.CombatId, e.Participants.Count);
        return Task.CompletedTask;
    }

    public Task Handle(CombatEnded e, CancellationToken ct)
    {
        _logger.LogInformation("Metric: Combat {CombatId} ended", e.CombatId);
        return Task.CompletedTask;
    }

    public Task Handle(SpellCast e, CancellationToken ct)
    {
        _logger.LogInformation("Metric: Spell {SpellId} cast by {CasterId}", e.SpellId, e.CasterId);
        return Task.CompletedTask;
    }

    public Task Handle(ExperienceGained e, CancellationToken ct)
    {
        _logger.LogInformation("Metric: Character {Id} gained {Amount} XP", e.CharacterId, e.Amount);
        return Task.CompletedTask;
    }

    public Task Handle(RestStarted e, CancellationToken ct)
    {
        _logger.LogInformation("Metric: Character {Id} started a {RestType} rest", e.CharacterId, e.RestType);
        return Task.CompletedTask;
    }

    public Task Handle(RestCompleted e, CancellationToken ct)
    {
        _logger.LogInformation("Metric: Character {Id} completed a {RestType} rest (HP restored: {Hp})", e.CharacterId, e.RestType, e.HitPointsRestored);
        return Task.CompletedTask;
    }

    public Task Handle(ConditionApplied e, CancellationToken ct)
    {
        _logger.LogInformation("Metric: Character {Id} gained condition {Condition}", e.CharacterId, e.Condition);
        return Task.CompletedTask;
    }

    public Task Handle(ConditionRemoved e, CancellationToken ct)
    {
        _logger.LogInformation("Metric: Character {Id} lost condition {Condition}", e.CharacterId, e.Condition);
        return Task.CompletedTask;
    }
}