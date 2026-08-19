// domain/events/movement_events.cs
namespace dnd_game.Domain.Events;

// ---------- Базовое перемещение ----------
public record CharacterMoved(Guid CharacterId, int FromX, int FromY, int ToX, int ToY, DateTime OccurredOn) : IDomainEvent;

// ---------- Перемещение с указанием типа ----------
public record CharacterMovedToPosition(Guid CharacterId, int TargetX, int TargetY, string MovementType, DateTime OccurredOn) : IDomainEvent;

// ---------- Действия, связанные с перемещением ----------
public record CharacterDashed(Guid CharacterId) : IDomainEvent;
public record CharacterDisengaged(Guid CharacterId) : IDomainEvent;
public record CharacterHid(Guid CharacterId) : IDomainEvent;

// ---------- Специальные виды движения ----------
public record CharacterClimbed(Guid CharacterId, int DistanceFeet, int ClimbSpeedUsed) : IDomainEvent;
public record CharacterSwam(Guid CharacterId, int DistanceFeet, int SwimSpeedUsed) : IDomainEvent;
public record CharacterFlew(Guid CharacterId, int DistanceFeet, int FlySpeedUsed) : IDomainEvent;
public record CharacterBurrowed(Guid CharacterId, int DistanceFeet, int BurrowSpeedUsed) : IDomainEvent;

// ---------- Прыжки ----------
public record CharacterJumped(Guid CharacterId, string JumpType, int StrengthScore, bool RunningStart, int DistanceFeet) : IDomainEvent;

// ---------- Управление скоростью ----------
public record CharacterSpeedChanged(Guid CharacterId, int NewSpeed, string MovementType) : IDomainEvent;
public record CharacterSpeedReset(Guid CharacterId) : IDomainEvent;

// ---------- Модификаторы местности ----------
public record DifficultTerrainApplied(Guid CharacterId, int Multiplier) : IDomainEvent;
public record DifficultTerrainRemoved(Guid CharacterId) : IDomainEvent;
public record MovementImpaired(Guid CharacterId, string ImpairmentType, int SpeedReduction) : IDomainEvent;
public record MovementRestored(Guid CharacterId, string ImpairmentType) : IDomainEvent;

// ---------- Проверки навыков, связанные с перемещением ----------
public record AthleticsCheckForMovementMade(Guid CharacterId, int DifficultyClass, int RollResult, int ProficiencyBonus, int StrengthModifier, bool Success) : IDomainEvent;
public record AcrobaticsCheckForMovementMade(Guid CharacterId, int DifficultyClass, int RollResult, int ProficiencyBonus, int DexterityModifier, bool Success) : IDomainEvent;

// ---------- Падение ----------
public record FallDamageTaken(Guid CharacterId, int FallDistanceFeet, int DamageAmount) : IDomainEvent;