// application/event_handlers/logging_handler.cs
using dnd_game.application.event_handlers;
using dnd_game.Domain.Events;
using Microsoft.Extensions.Logging;

namespace dnd_game.Application.EventHandlers;

public class LoggingHandler : IEventHandler<CharacterCreated>,
                              IEventHandler<CharacterDamageTaken>,
                              IEventHandler<CharacterHealed>,
                              IEventHandler<CharacterDied>,
                              IEventHandler<CombatStarted>,
                              IEventHandler<CombatEnded>,
                              IEventHandler<SpellCast>,
                              IEventHandler<ConditionApplied>,
                              IEventHandler<ConditionRemoved>
{
    private readonly ILogger<LoggingHandler> _logger;

    public LoggingHandler(ILogger<LoggingHandler> logger) => _logger = logger;

    public Task Handle(CharacterCreated e, CancellationToken ct)
    {
        _logger.LogInformation("Character created: {Name} ({Id})", e.Name, e.CharacterId);
        return Task.CompletedTask;
    }

    public Task Handle(CharacterDamageTaken e, CancellationToken ct)
    {
        _logger.LogInformation("Character {Id} takes {Amount} damage", e.CharacterId, e.Amount);
        return Task.CompletedTask;
    }

    public Task Handle(CharacterHealed e, CancellationToken ct)
    {
        _logger.LogInformation("Character {Id} healed for {Amount}", e.CharacterId, e.Amount);
        return Task.CompletedTask;
    }

    public Task Handle(CharacterDied e, CancellationToken ct)
    {
        _logger.LogWarning("Character {Id} has died!", e.CharacterId);
        return Task.CompletedTask;
    }

    public Task Handle(CombatStarted e, CancellationToken ct)
    {
        _logger.LogInformation("Combat {CombatId} started with {Count} participants", e.CombatId, e.Participants.Count);
        return Task.CompletedTask;
    }

    public Task Handle(CombatEnded e, CancellationToken ct)
    {
        _logger.LogInformation("Combat {CombatId} ended", e.CombatId);
        return Task.CompletedTask;
    }

    public Task Handle(SpellCast e, CancellationToken ct)
    {
        _logger.LogInformation("Caster {CasterId} cast spell {SpellId} (target: {TargetId})", e.CasterId, e.SpellId, e.TargetId);
        return Task.CompletedTask;
    }

    public Task Handle(ConditionApplied e, CancellationToken ct)
    {
        _logger.LogInformation("Character {Id} gained condition: {Condition}", e.CharacterId, e.Condition);
        return Task.CompletedTask;
    }

    public Task Handle(ConditionRemoved e, CancellationToken ct)
    {
        _logger.LogInformation("Character {Id} lost condition: {Condition}", e.CharacterId, e.Condition);
        return Task.CompletedTask;
    }
}