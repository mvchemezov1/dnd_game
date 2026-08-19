// domain/events/condition_events.cs
namespace dnd_game.Domain.Events;

// ---------- События с длительностью ----------
public record ConditionAppliedWithDuration(Guid CharacterId, string Condition, int DurationRounds, DateTime OccurredOn) : IDomainEvent;
public record ConditionDurationDecreased(Guid CharacterId, string Condition, int RemainingRounds) : IDomainEvent;
public record ConditionExpired(Guid CharacterId, string Condition) : IDomainEvent;

// ---------- События, связанные с источником состояния ----------
public record ConditionAppliedBySource(Guid CharacterId, string Condition, Guid SourceCharacterId, string SourceDescription, DateTime OccurredOn) : IDomainEvent;
public record ConditionRemovedBySource(Guid CharacterId, string Condition, Guid RemovedByCharacterId) : IDomainEvent;

// ---------- Спасброски против состояний ----------
public record ConditionSavingThrowAttempted(Guid CharacterId, string Condition, string Ability, int DifficultyClass, int RollResult, bool Success) : IDomainEvent;

// ---------- Состояния, связанные с концентрацией ----------
public record ConditionConcentrationRequired(Guid CharacterId, string Condition, string SpellId) : IDomainEvent;
public record ConditionConcentrationBroken(Guid CharacterId, string Condition) : IDomainEvent;

// ---------- Усталость (Exhaustion) ----------
public record ExhaustionLevelIncreased(Guid CharacterId, int NewExhaustionLevel) : IDomainEvent;
public record ExhaustionLevelDecreased(Guid CharacterId, int NewExhaustionLevel) : IDomainEvent;
public record ExhaustionRemoved(Guid CharacterId) : IDomainEvent;

// ---------- Отравление и болезни ----------
public record PoisonApplied(Guid CharacterId, string PoisonType, int DurationRounds, int SaveDC) : IDomainEvent;
public record PoisonSaveAttempted(Guid CharacterId, string PoisonType, int DC, int Roll, bool Success) : IDomainEvent;
public record DiseaseApplied(Guid CharacterId, string DiseaseName, int IncubationDays, int SaveDC) : IDomainEvent;
public record DiseaseProgressed(Guid CharacterId, string DiseaseName, int NewStage) : IDomainEvent;
public record DiseaseCured(Guid CharacterId, string DiseaseName) : IDomainEvent;

// ---------- Паралич, оцепенение, бессознательность ----------
public record ParalyzedConditionApplied(Guid CharacterId, int DurationRounds) : IDomainEvent;
public record StunnedConditionApplied(Guid CharacterId, int DurationRounds) : IDomainEvent;
public record UnconsciousConditionApplied(Guid CharacterId, string Reason) : IDomainEvent;
public record PetrifiedConditionApplied(Guid CharacterId) : IDomainEvent;

// ---------- Магические эффекты (очарование, страх и т.д.) ----------
public record CharmedConditionApplied(Guid CharacterId, Guid SourceCharacterId) : IDomainEvent;
public record FrightenedConditionApplied(Guid CharacterId, Guid SourceCharacterId) : IDomainEvent;
public record BlindedConditionApplied(Guid CharacterId, int DurationRounds) : IDomainEvent;
public record DeafenedConditionApplied(Guid CharacterId, int DurationRounds) : IDomainEvent;
public record InvisibleConditionApplied(Guid CharacterId) : IDomainEvent;

// ---------- Групповые состояния ----------
public record ConditionAppliedToMultiple(IEnumerable<Guid> CharacterIds, string Condition, int DurationRounds) : IDomainEvent;
public record ConditionRemovedFromMultiple(IEnumerable<Guid> CharacterIds, string Condition) : IDomainEvent;

// ---------- Снятие состояний при исцелении/отдыхе ----------
public record ConditionsClearedByRest(Guid CharacterId, IEnumerable<string> ConditionsRemoved) : IDomainEvent;
public record ConditionsClearedByHealing(Guid CharacterId, string Condition) : IDomainEvent;

// ---------- Сопротивления и иммунитеты (связанные с состояниями) ----------
public record ConditionResisted(Guid CharacterId, string Condition, string ResistanceSource) : IDomainEvent;
public record ConditionImmune(Guid CharacterId, string Condition) : IDomainEvent;