// domain/events/dialogue_events.cs
namespace dnd_game.Domain.Events;

// ---------- Управление диалогом ----------
public record DialogueStarted(Guid DialogueId, Guid NpcId, Guid CharacterId, DateTime OccurredOn) : IDomainEvent;
public record DialogueEnded(Guid DialogueId, DateTime OccurredOn) : IDomainEvent;

// ---------- Навигация по узлам ----------
public record DialogueNodeReached(Guid DialogueId, Guid NodeId, string NpcText, DateTime OccurredOn) : IDomainEvent;

// ---------- Выбор варианта ----------
public record DialogueOptionSelected(Guid DialogueId, Guid OptionId, string PlayerText, DateTime OccurredOn) : IDomainEvent;

// ---------- Проверки навыков в диалоге ----------
public record DialogueSkillCheckAttempted(Guid DialogueId, string SkillOrAbility, int DifficultyClass, DateTime OccurredOn) : IDomainEvent;
public record DialogueSkillCheckResolved(Guid DialogueId, string SkillOrAbility, int DifficultyClass, int RollResult, int TotalModifier, bool Success, DateTime OccurredOn) : IDomainEvent;

// ---------- Эффекты диалога ----------
public record DialogueEffectApplied(Guid DialogueId, string EffectType, Dictionary<string, string> Parameters, DateTime OccurredOn) : IDomainEvent;

// ---------- Результаты (успех/провал) ----------
public record DialogueOptionSucceeded(Guid DialogueId, Guid OptionId, DateTime OccurredOn) : IDomainEvent;
public record DialogueOptionFailed(Guid DialogueId, Guid OptionId, string Reason, DateTime OccurredOn) : IDomainEvent;