// infrastructure/event_store/event_store_interface.cs
using dnd_game.Domain.Aggregates;
using dnd_game.Domain.Events;

namespace dnd_game.Infrastructure.EventStore
{
    /// <summary>
    /// Метаданные события, сохраняемые вместе с событием в хранилище.
    /// </summary>
    public class EventMetadata
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public string EventType { get; set; } = string.Empty;
        public Guid AggregateId { get; set; }
        public int Version { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Guid UserId { get; set; }
        public Guid GameSessionId { get; set; }
        public Dictionary<string, string>? CustomHeaders { get; set; }
        public string AggregateType { get; set; } = string.Empty;   // <-- добавили
    }

    /// <summary>
    /// Запись хранимого события (событие + метаданные).
    /// </summary>
    public class StoredEvent
    {
        public IDomainEvent DomainEvent { get; set; } = null!;
        public EventMetadata Metadata { get; set; } = null!;
    }

    /// <summary>
    /// Снимок состояния агрегата (snapshot) для ускорения восстановления.
    /// </summary>
    public class Snapshot
    {
        public Guid AggregateId { get; set; }
        public int Version { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();   // сериализованное состояние агрегата
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Параметры чтения потока событий.
    /// </summary>
    public class ReadStreamOptions
    {
        public int FromVersion { get; set; } = 0;
        public int? MaxCount { get; set; }
        public bool ReadBackwards { get; set; } = false;
        public string? EventTypeFilter { get; set; }             // можно фильтровать по типу события
        public DateTime? FromTimestamp { get; set; }
    }

    /// <summary>
    /// Расширенный интерфейс Event Store, адаптированный к требованиям DnD.
    /// </summary>
    public interface IEventStore
    {
        // ---------- Базовые операции ----------
        Task<IEnumerable<object>> GetAllEvents();
        Task Save<T>(T aggregate, CancellationToken cancellationToken) where T : AggregateRoot, new();
        Task<T?> Load<T>(Guid aggregateId, CancellationToken cancellationToken) where T : AggregateRoot, new();
        Task<IEnumerable<object>> GetEvents(Guid aggregateId, int fromVersion = 0);

        // ---------- Сохранение с метаданными ----------
        /// <summary>
        /// Сохранить несохранённые события агрегата с заданными метаданными.
        /// </summary>
        Task SaveWithMetadata<T>(T aggregate, EventMetadata metadata) where T : AggregateRoot, new();

        // ---------- Чтение с метаданными и фильтрацией ----------
        /// <summary>
        /// Загрузить агрегат с возможностью получения истории событий с метаданными.
        /// </summary>
        Task<T?> LoadWithMetadata<T>(Guid aggregateId) where T : AggregateRoot, new();

        /// <summary>
        /// Получить поток событий для агрегата с метаданными.
        /// </summary>
        Task<IEnumerable<StoredEvent>> GetEventStreamAsync(Guid aggregateId, ReadStreamOptions? options = null);

        // ---------- Поддержка снапшотов ----------
        Task SaveSnapshotAsync(Snapshot snapshot);
        Task<Snapshot?> GetLatestSnapshotAsync(Guid aggregateId, int maxVersion);

        // ---------- Глобальные запросы ----------
        /// <summary>
        /// Получить все события определённого типа за заданный период (для проекций).
        /// </summary>
        Task<IEnumerable<StoredEvent>> GetEventsByTypeAsync(string eventType, DateTime? from = null, DateTime? to = null, int? maxCount = null);

        /// <summary>
        /// Получить все события, произошедшие в рамках конкретной игровой сессии (кампании).
        /// </summary>
        Task<IEnumerable<StoredEvent>> GetEventsBySessionAsync(Guid gameSessionId);

        // ---------- Управление версиями ----------
        /// <summary>
        /// Получить текущую версию агрегата (последний номер события).
        /// </summary>
        Task<int> GetCurrentVersionAsync(Guid aggregateId);

        // ---------- Потоковая подписка (для реактивных обработчиков) ----------
        /// <summary>
        /// Подписаться на все новые события, записываемые в Event Store.
        /// </summary>
        Task SubscribeAsync(Func<StoredEvent, CancellationToken, Task> handler, CancellationToken cancellationToken = default);
    }
}