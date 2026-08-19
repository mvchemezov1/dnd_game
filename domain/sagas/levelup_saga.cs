// domain/sagas/levelup_saga.cs
using dnd_game.Domain.Events;
using dnd_game.Domain.Commands;
using dnd_game.Application.Projections;
using dnd_game.Domain.Rules;
using dnd_game.infrastructure.message_bus;

namespace dnd_game.Domain.Sagas;

public class LevelUpSaga : ISaga
{
    private readonly ICommandBus _commandBus;
    private readonly CharacterProjection _characterProjection;
    private LevelUpSagaState _state;

    public LevelUpSaga(Guid characterId, ICommandBus commandBus, CharacterProjection characterProjection)
    {
        _commandBus = commandBus;
        _characterProjection = characterProjection;
        _state = new LevelUpSagaState { SagaId = characterId, CorrelationId = characterId };
    }

    public Guid SagaId => _state.SagaId;
    public ISagaState State => _state;

    public void LoadState(ISagaState state) => _state = (LevelUpSagaState)state;

    public Task Complete(bool success, string? reason = null, CancellationToken cancellationToken = default)
    {
        _state.Status = success ? SagaStatus.Completed : SagaStatus.Failed;
        return Task.CompletedTask;
    }

    private static readonly Dictionary<int, int> ExperienceThresholds = new()
    {
        {1, 0}, {2, 300}, {3, 900}, {4, 2700}, {5, 6500}, {6, 14000}, {7, 23000},
        {8, 34000}, {9, 48000}, {10, 64000}, {11, 85000}, {12, 100000}, {13, 120000},
        {14, 140000}, {15, 165000}, {16, 195000}, {17, 225000}, {18, 265000},
        {19, 305000}, {20, 355000}
    };

    public async Task Handle(IDomainEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event is ExperienceGained expGained)
        {
            await ProcessExperienceGain(expGained, cancellationToken);
        }
    }

    private async Task ProcessExperienceGain(ExperienceGained e, CancellationToken cancellationToken)
    {
        _state.Status = SagaStatus.InProgress;

        var character = await _characterProjection.GetById(e.CharacterId);
        if (character == null)
        {
            _state.Status = SagaStatus.Failed;
            return;
        }

        int currentLevel = character.Level;
        int currentXp = character.ExperiencePoints;

        int maxPossibleLevel = currentLevel;
        for (int lvl = currentLevel + 1; lvl <= 20; lvl++)
        {
            if (currentXp >= ExperienceThresholds[lvl])
                maxPossibleLevel = lvl;
            else
                break;
        }

        if (maxPossibleLevel <= currentLevel)
        {
            _state.Status = SagaStatus.Completed;
            return;
        }

        for (int newLevel = currentLevel + 1; newLevel <= maxPossibleLevel; newLevel++)
        {
            await ApplyLevelUp(e.CharacterId, newLevel, cancellationToken);
            _state.LastAppliedLevel = newLevel;
        }

        _state.Status = SagaStatus.Completed;
    }

    private async Task ApplyLevelUp(Guid characterId, int newLevel, CancellationToken cancellationToken)
    {
        await _commandBus.SendAsync(new LevelUpCharacter(characterId, newLevel), new CommandContext { CancellationToken = cancellationToken });

        int hitDieType = 8;
        var character = await _characterProjection.GetById(characterId);
        int conScore = character?.AbilityScores?.GetValueOrDefault("Constitution", 10) ?? 10;
        int conModifier = (conScore - 10) / 2;

        int averageRoll = hitDieType / 2 + 1;
        int hpIncrease = averageRoll + conModifier;
        await _commandBus.SendAsync(new IncreaseMaxHitPoints(characterId, hpIncrease), new CommandContext { CancellationToken = cancellationToken });
        await _commandBus.SendAsync(new AddHitDie(characterId, hitDieType), new CommandContext { CancellationToken = cancellationToken });

        await UpdateSpellSlots(characterId, newLevel, cancellationToken);
    }

    private async Task UpdateSpellSlots(Guid characterId, int level, CancellationToken cancellationToken)
    {
        var slots = MagicRules.FullCasterSpellSlots(level);
        if (slots.Count > 0)
            await _commandBus.SendAsync(new SetSpellSlots(characterId, slots), new CommandContext { CancellationToken = cancellationToken });
    }

    private class LevelUpSagaState : ISagaState
    {
        public Guid SagaId { get; set; }
        public Guid CorrelationId { get; set; }
        public SagaStatus Status { get; set; } = SagaStatus.Started;
        public int Version { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public int LastAppliedLevel { get; set; }
    }
}