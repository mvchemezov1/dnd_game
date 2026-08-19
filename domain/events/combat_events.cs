// domain/events/combat_events.cs
namespace dnd_game.Domain.Events;

// ---------- Управление боем ----------
public record CombatStarted(Guid CombatId, List<Guid> Participants, DateTime OccurredOn) : IDomainEvent;
public record CombatEnded(Guid CombatId, DateTime OccurredOn) : IDomainEvent;

// ---------- Инициатива и раунды ----------
public record InitiativeRolled(Guid CombatId, Guid CharacterId, int Initiative, int DexterityModifier) : IDomainEvent;
public record CombatRoundStarted(Guid CombatId, int Round, DateTime OccurredOn) : IDomainEvent;

// ---------- Ходы ----------
public record CombatTurnStarted(Guid CombatId, Guid CharacterId, DateTime OccurredOn) : IDomainEvent;
public record CombatTurnEnded(Guid CombatId, Guid CharacterId, DateTime OccurredOn) : IDomainEvent;

// ---------- Участники ----------
public record ParticipantAddedToCombat(Guid CombatId, Guid CharacterId, int Initiative) : IDomainEvent;
public record ParticipantRemovedFromCombat(Guid CombatId, Guid CharacterId) : IDomainEvent;

// ---------- Действия ----------
public record CombatActionTaken(Guid CombatId, Guid CharacterId) : IDomainEvent;          // основное действие использовано
public record CombatBonusActionTaken(Guid CombatId, Guid CharacterId) : IDomainEvent;      // бонусное действие
public record CombatReactionUsed(Guid CombatId, Guid CharacterId) : IDomainEvent;          // реакция потрачена

// ---------- Перемещение ----------
public record CombatMovementUsed(Guid CombatId, Guid CharacterId, int Feet) : IDomainEvent;

// ---------- Состояния ----------
public record ConditionAppliedToCombatant(Guid CombatId, Guid CharacterId, string Condition) : IDomainEvent;
public record ConditionRemovedFromCombatant(Guid CombatId, Guid CharacterId, string Condition) : IDomainEvent;

// ---------- Концентрация ----------
public record CombatConcentrationStarted(Guid CombatId, Guid CharacterId) : IDomainEvent;
public record CombatConcentrationEnded(Guid CombatId, Guid CharacterId) : IDomainEvent;
public record CombatDamageDealt(Guid CombatId, Guid SourceId, Guid TargetId, int Amount, string DamageType) : IDomainEvent;
public record CombatHealingDealt(Guid CombatId, Guid SourceId, Guid TargetId, int Amount) : IDomainEvent;
public record CombatSavingThrowMade(Guid CombatId, Guid ParticipantId, string Ability, int DC, int Roll, int Modifiers) : IDomainEvent;
public record CombatDeathSavingThrowMade(Guid CombatId, Guid ParticipantId, int Roll) : IDomainEvent;
public record CombatParticipantStabilized(Guid CombatId, Guid ParticipantId, Guid StabilizedBy) : IDomainEvent;
public record CombatConcentrationCheckMade(Guid CombatId, Guid ParticipantId, int DC, int Roll, int ConMod) : IDomainEvent;
public record CombatTurnDelayed(Guid CombatId, Guid ParticipantId) : IDomainEvent;
public record CombatSurrender(Guid CombatId, Guid ParticipantId) : IDomainEvent;