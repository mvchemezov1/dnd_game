// infrastructure/event_store/event_stream.cs
using dnd_game.Domain.Events;
using dnd_game.Domain.Exceptions;

namespace dnd_game.Infrastructure.EventStore
{
    /// <summary>
    /// Представляет поток событий одного агрегата, включая метаданные и версионирование.
    /// Поддерживает добавление событий с проверкой версий, загрузку из истории,
    /// и формирование снапшотов для ускорения восстановления.
    /// </summary>
    public class EventStream
    {
        /// <summary>Идентификатор агрегата, которому принадлежит поток.</summary>
        public Guid AggregateId { get; set; }

        /// <summary>Текущая версия агрегата (количество применённых событий).</summary>
        public int Version { get; set; }

        /// <summary>Тип агрегата (например, "CharacterAggregate", "CombatAggregate").</summary>
        public string AggregateType { get; set; } = string.Empty;

        /// <summary>Список записей событий, каждая с событием и метаданными.</summary>
        public List<StoredEvent> Events { get; set; } = new();

        /// <summary>Временная метка создания потока.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Временная метка последнего изменения.</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // ---------- Добавление событий ----------

        /// <summary>
        /// Добавить одно доменное событие с метаданными в конец потока.
        /// Автоматически увеличивает версию и устанавливает её в метаданных.
        /// </summary>
        /// <param name="domainEvent">Доменное событие.</param>
        /// <param name="metadata">Метаданные события (версия будет перезаписана).</param>
        public void Append(IDomainEvent domainEvent, EventMetadata metadata)
        {
            if (domainEvent == null) throw new ArgumentNullException(nameof(domainEvent));
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));

            Version++;
            metadata.Version = Version;
            metadata.AggregateId = AggregateId;
            metadata.Timestamp = DateTime.UtcNow;

            Events.Add(new StoredEvent
            {
                DomainEvent = domainEvent,
                Metadata = metadata
            });

            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Добавить несколько событий пачкой. Версия увеличивается последовательно.
        /// </summary>
        public void AppendRange(IEnumerable<IDomainEvent> domainEvents, EventMetadata metadataTemplate)
        {
            foreach (var e in domainEvents)
            {
                // Клонируем метаданные, чтобы у каждого события был уникальный EventId
                var meta = new EventMetadata
                {
                    EventId = Guid.NewGuid(),
                    EventType = e.GetType().Name,
                    AggregateId = AggregateId,
                    UserId = metadataTemplate.UserId,
                    GameSessionId = metadataTemplate.GameSessionId,
                    CustomHeaders = metadataTemplate.CustomHeaders == null
                        ? null
                        : new Dictionary<string, string>(metadataTemplate.CustomHeaders)
                };
                Append(e, meta);
            }
        }

        // ---------- Проверка версий (оптимистическая блокировка) ----------

        /// <summary>
        /// Проверить, что ожидаемая версия совпадает с текущей.
        /// Бросает <see cref="StateConflictException"/> при несовпадении.
        /// </summary>
        public void AssertExpectedVersion(int expectedVersion)
        {
            if (expectedVersion != Version)
                throw new StateConflictException(AggregateId, expectedVersion, Version);
        }

        // ---------- Получение событий в виде доменных объектов ----------

        /// <summary>
        /// Получить все доменные события (без метаданных) для восстановления агрегата.
        /// </summary>
        public IEnumerable<IDomainEvent> GetDomainEvents()
        {
            return Events.Select(e => e.DomainEvent);
        }

        /// <summary>
        /// Получить события начиная с указанной версии (для дозагрузки).
        /// </summary>
        public IEnumerable<StoredEvent> GetEventsFromVersion(int fromVersion)
        {
            // Версии нумеруются с 1; пропускаем события с версией <= fromVersion
            return Events.Where(e => e.Metadata.Version > fromVersion);
        }

        // ---------- Снапшоты ----------

        /// <summary>
        /// Создать снапшот текущего состояния агрегата (сериализованное состояние).
        /// Вызывается внешним кодом, который знает, как сериализовать агрегат.
        /// </summary>
        public Snapshot CreateSnapshot(byte[] serializedAggregateState)
        {
            return new Snapshot
            {
                AggregateId = AggregateId,
                Version = Version,
                Data = serializedAggregateState,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Применить снапшот: установить версию и временную метку создания, если снапшот новее.
        /// </summary>
        public void ApplySnapshot(Snapshot snapshot)
        {
            if (snapshot.AggregateId != AggregateId)
                throw new ArgumentException("Snapshot does not belong to this stream.");
            Version = snapshot.Version;
            UpdatedAt = snapshot.CreatedAt;
            // События не очищаются, так как снапшот не удаляет историю.
        }

        // ---------- Вспомогательные методы ----------

        /// <summary>
        /// Получить последнее событие потока (может быть null).
        /// </summary>
        public StoredEvent? GetLastEvent()
        {
            return Events.LastOrDefault();
        }

        /// <summary>
        /// Получить количество событий в потоке.
        /// </summary>
        public int EventCount => Events.Count;

        /// <summary>
        /// Создать копию потока без событий (для нового агрегата).
        /// </summary>
        public static EventStream New(Guid aggregateId, string aggregateType)
        {
            return new EventStream
            {
                AggregateId = aggregateId,
                AggregateType = aggregateType,
                Version = 0,
                CreatedAt = DateTime.UtcNow
            };
        }

        public override string ToString()
        {
            return $"EventStream({AggregateType}:{AggregateId} v{Version}, {EventCount} events)";
        }
    }
}