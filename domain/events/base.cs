// domain/events/base.cs
namespace dnd_game.Domain.Events;

// --------------------------------------------------------------------------------------------
// Базовый интерфейс (оставлен без изменений)
// --------------------------------------------------------------------------------------------
public interface IDomainEvent { }

// --------------------------------------------------------------------------------------------
// Событие с метаданными (временная метка, источник)
// --------------------------------------------------------------------------------------------

/// <summary>
/// Событие, содержащее обязательную временную метку возникновения.
/// Все события, сохраняемые в EventStore, должны реализовывать этот интерфейс.
/// </summary>
public interface ITimestampedEvent : IDomainEvent
{
    DateTime OccurredOn { get; }
}

/// <summary>
/// Событие, инициированное конкретным пользователем (игроком или мастером).
/// </summary>
public interface IUserInitiatedEvent : IDomainEvent
{
    Guid UserId { get; }
}

/// <summary>
/// Событие, относящееся к определённой игровой сессии (кампании).
/// </summary>
public interface ISessionBoundEvent : IDomainEvent
{
    Guid GameSessionId { get; }
}

// --------------------------------------------------------------------------------------------
// События, связанные с агрегатами
// --------------------------------------------------------------------------------------------

/// <summary>
/// Событие, принадлежащее конкретному агрегату.
/// </summary>
public interface IAggregateEvent : IDomainEvent
{
    Guid AggregateId { get; }
}

/// <summary>
/// Событие, которое несёт версию агрегата после применения.
/// Используется для оптимистической блокировки и воспроизведения.
/// </summary>
public interface IVersionedEvent : IAggregateEvent
{
    int Version { get; }
}

// --------------------------------------------------------------------------------------------
// События, связанные с персонажем
// --------------------------------------------------------------------------------------------

/// <summary>
/// Событие, затрагивающее конкретного персонажа.
/// </summary>
public interface ICharacterEvent : IAggregateEvent
{
    Guid CharacterId { get; }
}

/// <summary>
/// Событие, связанное с действием одного персонажа по отношению к другому (атака, лечение и т.д.).
/// </summary>
public interface ICharacterInteractionEvent : ICharacterEvent
{
    Guid SourceCharacterId { get; }
    Guid TargetCharacterId { get; }
}

// --------------------------------------------------------------------------------------------
// События боя
// --------------------------------------------------------------------------------------------

/// <summary>
/// Событие, относящееся к конкретному бою.
/// </summary>
public interface ICombatEvent : IAggregateEvent
{
    Guid CombatId { get; }
}

/// <summary>
/// Событие, связанное с действием участника боя.
/// </summary>
public interface ICombatActionEvent : ICombatEvent
{
    Guid ParticipantId { get; }
}

// --------------------------------------------------------------------------------------------
// События кампании
// --------------------------------------------------------------------------------------------

/// <summary>
/// Событие, относящееся к конкретной кампании.
/// </summary>
public interface ICampaignEvent : IAggregateEvent
{
    Guid CampaignId { get; }
}

// --------------------------------------------------------------------------------------------
// Базовый абстрактный класс события (опционально, для удобства)
// --------------------------------------------------------------------------------------------

/// <summary>
/// Удобная базовая реализация доменного события.
/// Наследники получают стандартные метаданные и могут быть сериализованы.
/// </summary>
public abstract record BaseDomainEvent : ITimestampedEvent, IAggregateEvent
{
    public Guid AggregateId { get; init; }
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Базовое событие, связанное с персонажем.
/// </summary>
public abstract record CharacterDomainEvent : BaseDomainEvent, ICharacterEvent
{
    public Guid CharacterId => AggregateId;
}

/// <summary>
/// Базовое событие, связанное с боевой сценой.
/// </summary>
public abstract record CombatDomainEvent : BaseDomainEvent, ICombatEvent
{
    public Guid CombatId => AggregateId;
}

/// <summary>
/// Базовое событие, связанное с кампанией.
/// </summary>
public abstract record CampaignDomainEvent : BaseDomainEvent, ICampaignEvent
{
    public Guid CampaignId => AggregateId;
}