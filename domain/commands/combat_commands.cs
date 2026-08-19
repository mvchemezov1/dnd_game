// domain/commands/combat_commands.cs
namespace dnd_game.Domain.Commands;

// ---------- Управление боем ----------
public record StartCombat(Guid CombatId, List<Guid> Participants) : ICommand;
public record EndCombat(Guid CombatId) : ICommand;

// ---------- Инициатива ----------
public record RollInitiative(Guid CombatId, Guid ParticipantId, int InitiativeRoll, int DexterityModifier) : ICommand;

// ---------- Раунды и ходы ----------
public record StartRound(Guid CombatId) : ICommand;
public record NextTurn(Guid CombatId) : ICommand;
public record EndRound(Guid CombatId) : ICommand;

// ---------- Участники ----------
public record AddParticipantToCombat(Guid CombatId, Guid ParticipantId, int Initiative) : ICommand;
public record RemoveParticipantFromCombat(Guid CombatId, Guid ParticipantId) : ICommand;

// ---------- Действия ----------
public record TakeStandardAction(Guid CombatId, Guid ParticipantId, string ActionType, Guid? TargetId = null, object? ActionData = null) : ICommand;
public record TakeBonusAction(Guid CombatId, Guid ParticipantId, string ActionType, Guid? TargetId = null, object? ActionData = null) : ICommand;
public record TakeReaction(Guid CombatId, Guid ParticipantId, string ReactionType, string TriggerDescription, Guid? TargetId = null) : ICommand;

// ---------- Перемещение ----------
public record TakeMoveAction(Guid CombatId, Guid ParticipantId, int DistanceFeet) : ICommand;

// ---------- Готовое действие ----------
public record ReadyAction(Guid CombatId, Guid ParticipantId, string ActionToReady, string TriggerCondition) : ICommand;
public record TriggerReadyAction(Guid CombatId, Guid ParticipantId) : ICommand;

// ---------- Урон и лечение ----------
public record DealDamageToTarget(Guid CombatId, Guid SourceParticipantId, Guid TargetParticipantId, int DamageAmount, string DamageType) : ICommand;
public record HealTarget(Guid CombatId, Guid SourceParticipantId, Guid TargetParticipantId, int HealingAmount) : ICommand;

// ---------- Состояния ----------
public record ApplyConditionToTarget(Guid CombatId, Guid TargetParticipantId, string ConditionType, int DurationRounds) : ICommand;
public record RemoveConditionFromTarget(Guid CombatId, Guid TargetParticipantId, string ConditionType) : ICommand;

// ---------- Спасброски ----------
public record MakeSavingThrowInCombat(Guid CombatId, Guid ParticipantId, string Ability, int DifficultyClass, int RollResult, int Modifiers) : ICommand;
public record MakeDeathSavingThrowInCombat(Guid CombatId, Guid ParticipantId, int RollResult) : ICommand;
public record StabilizeInCombat(Guid CombatId, Guid ParticipantId, Guid StabilizedByParticipantId) : ICommand;

// ---------- Концентрация ----------
public record MakeConcentrationCheck(Guid CombatId, Guid ParticipantId, int DifficultyClass, int RollResult, int ConstitutionModifier) : ICommand;

// ---------- Особые действия ----------
public record DelayTurn(Guid CombatId, Guid ParticipantId) : ICommand;
public record SurrenderInCombat(Guid CombatId, Guid ParticipantId) : ICommand;

// ---------- Вспомогательные действия (не требуют отдельных команд, но для полноты) ----------
public record HelpAction(Guid CombatId, Guid HelperId, Guid TargetId) : ICommand;
public record HideAction(Guid CombatId, Guid HiderId) : ICommand;
public record SearchAction(Guid CombatId, Guid SearcherId) : ICommand;
public record UseObjectAction(Guid CombatId, Guid UserId, Guid ObjectId) : ICommand;
/// <summary>
/// Общая команда для выполнения любого боевого действия.
/// </summary>
public record PerformAction(
    Guid CombatId,
    Guid ParticipantId,
    string ActionType,          // например, "Attack", "CastSpell", "Dash", "Disengage" и т.д.
    Guid? TargetId = null,
    object? ActionData = null   // дополнительные данные (например, заклинание, оружие)
) : ICommand;