// domain/commands/movement_commands.cs
namespace dnd_game.Domain.Commands;

// Базовое перемещение на тактической карте
public record MoveCharacter(Guid CharacterId, int TargetX, int TargetY) : ICommand;
public record MoveCharacterToPosition(Guid CharacterId, int TargetX, int TargetY, string MovementType) : ICommand;

// Действия, связанные с перемещением
public record MoveCharacterWithDash(Guid CharacterId) : ICommand;
public record MoveCharacterWithDisengage(Guid CharacterId) : ICommand;
public record MoveCharacterStealthily(Guid CharacterId) : ICommand; // Hide

// Специальные виды движения
public record ClimbCharacter(Guid CharacterId, int DistanceFeet, int ClimbSpeedUsed = 0) : ICommand;
public record SwimCharacter(Guid CharacterId, int DistanceFeet, int SwimSpeedUsed = 0) : ICommand;
public record FlyCharacter(Guid CharacterId, int DistanceFeet, int FlySpeedUsed = 0) : ICommand;
public record BurrowCharacter(Guid CharacterId, int DistanceFeet, int BurrowSpeedUsed = 0) : ICommand;

// Прыжки
public record JumpCharacter(Guid CharacterId, string JumpType, int StrengthScore, bool RunningStart) : ICommand;

// Управление скоростью
public record SetCharacterSpeed(Guid CharacterId, int NewSpeed, string MovementType = "Walk") : ICommand;
public record ResetCharacterSpeed(Guid CharacterId) : ICommand;

// Модификаторы местности и окружения
public record ApplyDifficultTerrain(Guid CharacterId, int Multiplier) : ICommand;
public record RemoveDifficultTerrain(Guid CharacterId) : ICommand;
public record ApplyMovementImpairment(Guid CharacterId, string ImpairmentType, int SpeedReduction) : ICommand;
public record RemoveMovementImpairment(Guid CharacterId, string ImpairmentType) : ICommand;

// Проверки навыков, связанные с перемещением
public record MakeAthleticsCheckForMovement(Guid CharacterId, int DifficultyClass, int RollResult, int ProficiencyBonus, int StrengthModifier) : ICommand;
public record MakeAcrobaticsCheckForMovement(Guid CharacterId, int DifficultyClass, int RollResult, int ProficiencyBonus, int DexterityModifier) : ICommand;

// Падение и урон от падения
public record TakeFallDamage(Guid CharacterId, int FallDistanceFeet) : ICommand;
public record StartJourneyCommand(Guid PartyId, Guid RouteId, string Pace) : ICommand;
public record EndJourneyCommand(Guid PartyId) : ICommand;
public record TravelDayCommand(Guid PartyId, string Terrain, int HoursTraveled, int NavigationCheckResult) : ICommand;
public record SetTravelPaceCommand(Guid PartyId, string Pace) : ICommand;
public record ForcedMarchCommand(Guid PartyId, int AdditionalHours) : ICommand;
public record NavigationCheckCommand(Guid PartyId, int Roll, int WisdomModifier, bool IsProficient) : ICommand;
public record PartyLostCommand(Guid PartyId) : ICommand;
public record ConsumeResourcesCommand(Guid PartyId, int Days) : ICommand;
public record RandomEncounterCheckCommand(Guid PartyId, string Terrain) : ICommand;
public record ApplyExhaustionCommand(Guid PartyId, int ExhaustionLevel) : ICommand;