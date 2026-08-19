// domain/events/script_events.cs
namespace dnd_game.Domain.Events;

// ---------- Базовое событие срабатывания триггера (оставлено) ----------
public record ScriptTriggered(Guid ScriptId, string TriggerName, Dictionary<string, string> Parameters, DateTime Timestamp) : IDomainEvent;

// ---------- Управление состоянием триггеров ----------
public record ScriptTriggerEnabled(Guid ScriptId, string TriggerName, DateTime Timestamp) : IDomainEvent;
public record ScriptTriggerDisabled(Guid ScriptId, string TriggerName, DateTime Timestamp) : IDomainEvent;

// ---------- Выполнение скрипта ----------
public record ScriptExecutionStarted(Guid ScriptId, string TriggerName, DateTime Timestamp) : IDomainEvent;
public record ScriptExecutionCompleted(Guid ScriptId, string TriggerName, DateTime Timestamp) : IDomainEvent;
public record ScriptExecutionFailed(Guid ScriptId, string TriggerName, string ErrorMessage, DateTime Timestamp) : IDomainEvent;

// ---------- Проверка условий ----------
public record ScriptConditionEvaluated(Guid ScriptId, string ConditionType, Dictionary<string, string> Parameters, bool Result, DateTime Timestamp) : IDomainEvent;

// ---------- Выполнение отдельных действий ----------
public record ScriptActionExecuted(Guid ScriptId, string ActionType, Dictionary<string, string> Parameters, DateTime Timestamp) : IDomainEvent;

// ---------- Управление паузами и перезапусками ----------
public record ScriptPaused(Guid ScriptId, DateTime Timestamp) : IDomainEvent;
public record ScriptResumed(Guid ScriptId, DateTime Timestamp) : IDomainEvent;
public record ScriptReset(Guid ScriptId, string TriggerName, DateTime Timestamp) : IDomainEvent;