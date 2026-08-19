// application/event_handlers/replay_handler.cs
using dnd_game.Domain.Events;
using Microsoft.Extensions.Logging;

namespace dnd_game.Application.EventHandlers
{
    /// <summary>
    /// Хранилище событий с расширенными возможностями для воспроизведения.
    /// </summary>
    public interface IReplayEventStore
    {
        Task AppendAsync(IDomainEvent @event, ReplayMetadata metadata);
        Task<IEnumerable<IDomainEvent>> GetEventsAsync(Guid aggregateId, DateTime? toTimestamp = null);
        Task<IEnumerable<IDomainEvent>> GetEventsBySessionAsync(Guid sessionId);
        Task<long> GetEventCountAsync(Guid aggregateId);
        Task<IDomainEvent?> GetLastEventAsync(Guid aggregateId);
    }

    /// <summary>
    /// Метаданные события для целей воспроизведения.
    /// </summary>
    public class ReplayMetadata
    {
        public Guid SessionId { get; set; }
        public DateTime Timestamp { get; set; }
        public long SequenceNumber { get; set; }
        public string? Description { get; set; } // краткое описание для журнала
    }

    /// <summary>
    /// Сервис, предоставляющий текущую игровую сессию.
    /// </summary>
    public interface ICurrentSessionProvider
    {
        Guid GetCurrentSessionId();
    }

    /// <summary>
    /// Сервис для построения текстового журнала из доменных событий.
    /// </summary>
    public interface INarrativeLogBuilder
    {
        string BuildEntry(IDomainEvent @event);
    }

    public class ReplayHandler : IEventHandler<IDomainEvent>
    {
        private readonly IReplayEventStore _eventStore;
        private readonly ICurrentSessionProvider _sessionProvider;
        private readonly INarrativeLogBuilder _narrativeBuilder;
        private readonly ILogger<ReplayHandler> _logger;
        private long _globalSequence = 0;

        public ReplayHandler(
            IReplayEventStore eventStore,
            ICurrentSessionProvider sessionProvider,
            INarrativeLogBuilder narrativeBuilder,
            ILogger<ReplayHandler> logger)
        {
            _eventStore = eventStore;
            _sessionProvider = sessionProvider;
            _narrativeBuilder = narrativeBuilder;
            _logger = logger;
        }

        public async Task Handle(IDomainEvent @event, CancellationToken cancellationToken)
        {
            // Получаем текущий идентификатор сессии (кампании)
            var sessionId = _sessionProvider.GetCurrentSessionId();

            // Атомарно увеличиваем глобальный счётчик событий для сквозной нумерации
            var sequenceNumber = Interlocked.Increment(ref _globalSequence);

            // Строим описание события для человеко-читаемого журнала
            var description = _narrativeBuilder.BuildEntry(@event);

            // Формируем метаданные
            var metadata = new ReplayMetadata
            {
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow,
                SequenceNumber = sequenceNumber,
                Description = description
            };

            // Сохраняем событие с метаданными в хранилище
            await _eventStore.AppendAsync(@event, metadata);

            // Логируем факт записи для диагностики
            _logger.LogTrace("Replay event #{Sequence}: {EventType} (session {SessionId})",
                sequenceNumber, @event.GetType().Name, sessionId);
        }
    }
}