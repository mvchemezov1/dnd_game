// domain/events/rest_events.cs
namespace dnd_game.Domain.Events;

// ---------- Начало отдыха (с указанием типа) ----------
public record RestStarted(Guid CharacterId, string RestType, DateTime Timestamp) : IDomainEvent;

// ---------- Прерывание отдыха ----------
public record RestInterrupted(Guid CharacterId, string InterruptionType, DateTime Timestamp) : IDomainEvent;

// ---------- Завершение отдыха ----------
public record RestCompleted(Guid CharacterId, string RestType, int HitPointsRestored, DateTime Timestamp) : IDomainEvent;