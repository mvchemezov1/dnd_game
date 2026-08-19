// infrastructure/message_bus/event_bus.cs
using dnd_game.Domain.Events;
using dnd_game.infrastructure.message_bus;
using dnd_game.application.event_handlers;

namespace dnd_game.Infrastructure.MessageBus
{
    /// <summary>
    /// Универсальная шина событий для игры DnD.
    /// Публикация событий может приводить к нескольким подписчикам,
    /// синхронную обработку (проекции, кеши) и асинхронную рассылку.
    ///
    /// Единственные production-реализации: InMemoryBus (без брокера) и RabbitMqBus
    /// (см. in_memory_bus.cs / rabbitmq_bus.cs). Обе регистрируются в DI (dependencies.cs)
    /// напрямую под этот интерфейс — не добавляйте новых реализаций без крайней необходимости.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Публикация одного события подписчикам.
        /// </summary>
        Task PublishAsync(IDomainEvent @event, CancellationToken cancellationToken = default);

        /// <summary>
        /// Публикация набора событий (например, всех событий одного агрегата).
        /// </summary>
        Task PublishAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default);

        /// <summary>
        /// Публикация события с контекстом текущего пользователя.
        /// </summary>
        Task PublishAsync(IDomainEvent @event, CommandContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Подписка обработчика на конкретный тип события.
        /// </summary>
        void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IDomainEvent;

        /// <summary>
        /// Подписка делегата-обработчика.
        /// </summary>
        void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : IDomainEvent;

        /// <summary>
        /// Отписка от событий.
        /// </summary>
        void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IDomainEvent;
    }
}
