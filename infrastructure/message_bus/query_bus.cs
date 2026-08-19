// infrastructure/message_bus/query_bus.cs
using dnd_game.Domain.Queries;

namespace dnd_game.Infrastructure.MessageBus
{
    /// <summary>
    /// Контекст выполнения запроса (аналогично CommandContext).
    /// </summary>
    public class QueryContext
    {
        public Guid UserId { get; set; }
        public Guid GameSessionId { get; set; }
    }

    /// <summary>
    /// Универсальная шина запросов игры для DnD.
    ///
    /// Единственная production-реализация: InMemoryBus (см. in_memory_bus.cs), которая
    /// регистрируется в DI (dependencies.cs) напрямую под этот интерфейс — не добавляйте
    /// новых реализаций без крайней необходимости.
    /// </summary>
    public interface IQueryBus
    {
        /// <summary>
        /// Выполнить запрос и вернуть результат.
        /// </summary>
        Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, QueryContext? context = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Выполнить запрос с пагинацией (если запрос реализует IPagedQuery).
        /// </summary>
        Task<PagedResult<TResult>> QueryPagedAsync<TResult>(IPagedQuery<TResult> query, QueryContext? context = null, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Интерфейс pipeline-поведения для обработки запросов (middleware).
    /// Реализован в infrastructure/monitoring/logging_middleware.cs (LoggingMiddleware).
    /// Примечание: на данный момент InMemoryBus не выполняет pipeline поведения при
    /// выполнении запросов — интерфейс подготовлен, но не подключён к диспетчеризации.
    /// </summary>
    public interface IQueryPipelineBehavior
    {
        Task<TResult> HandleAsync<TResult>(IQuery<TResult> query, QueryContext context, Func<Task<TResult>> next);
    }
}
