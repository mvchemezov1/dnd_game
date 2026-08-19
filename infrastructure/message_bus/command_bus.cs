// infrastructure/message_bus/command_bus.cs
using dnd_game.Domain.Commands;

namespace dnd_game.infrastructure.message_bus
{
    /// <summary>
    /// Контекст выполнения команды, содержащий информацию о пользователе и сессии.
    /// </summary>
    public class CommandContext
    {
        public Guid UserId { get; set; }
        public Guid GameSessionId { get; set; }
        public CancellationToken CancellationToken { get; set; }
    }

    /// <summary>
    /// Универсальная шина команд игры для DnD.
    ///
    /// Единственные production-реализации: InMemoryBus (без брокера) и RabbitMqBus
    /// (см. in_memory_bus.cs / rabbitmq_bus.cs). Обе регистрируются в DI (dependencies.cs)
    /// напрямую под этот интерфейс — не добавляйте новых реализаций без крайней необходимости,
    /// чтобы не возвращать дублирование шин.
    /// </summary>
    public interface ICommandBus
    {
        Task SendAsync(ICommand command, CommandContext? context = null);
        void Subscribe<TCommand>(Func<TCommand, CommandContext?, Task> handler) where TCommand : ICommand;
    }

    /// <summary>
    /// Интерфейс pipeline-поведения для обработки команд (middleware).
    /// Реализован в infrastructure/monitoring/logging_middleware.cs (LoggingMiddleware).
    /// Примечание: на данный момент ни InMemoryBus, ни RabbitMqBus не выполняют pipeline
    /// поведения при отправке команд — интерфейс подготовлен, но не подключён к диспетчеризации.
    /// </summary>
    public interface ICommandPipelineBehavior
    {
        Task HandleAsync<TCommand>(TCommand command, CommandContext context, Func<Task> next) where TCommand : ICommand;
    }
}
