// domain/sagas/base.cs
using dnd_game.Domain.Commands;
using dnd_game.Domain.Events;
using dnd_game.infrastructure.message_bus;

namespace dnd_game.Domain.Sagas
{
    /// <summary>
    /// Состояние саги, используемое для сохранения прогресса длительного процесса.
    /// </summary>
    public interface ISagaState
    {
        /// <summary>Идентификатор экземпляра саги.</summary>
        Guid SagaId { get; }

        /// <summary>Идентификатор корреляции (например, Id боя или торговой сделки).</summary>
        Guid CorrelationId { get; }

        /// <summary>Текущий статус саги.</summary>
        SagaStatus Status { get; set; }

        /// <summary>Версия состояния для оптимистической блокировки.</summary>
        int Version { get; set; }

        /// <summary>Дата и время создания саги.</summary>
        DateTime CreatedAt { get; }

        /// <summary>Дата и время последнего изменения.</summary>
        DateTime? UpdatedAt { get; set; }
    }

    public enum SagaStatus
    {
        Started,
        InProgress,
        Completed,
        Failed,
        Compensating,
        Compensated,
        Cancelled
    }

    /// <summary>
    /// Интерфейс саги, способной обрабатывать доменные события и управлять состоянием.
    /// </summary>
    public interface ISaga
    {
        Guid SagaId { get; }
        ISagaState State { get; }
        void LoadState(ISagaState state);       // <-- добавить
        Task Handle(IDomainEvent @event, CancellationToken cancellationToken = default);
        Task Complete(bool success, string? reason = null, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Сага, способная отправлять команды для выполнения шагов процесса.
    /// </summary>
    public interface ICommandingSaga : ISaga
    {
        Task SendCommand(ICommand command, CancellationToken cancellationToken = default);
        void SetCommandBus(ICommandBus commandBus); // добавлено
    }

    /// <summary>
    /// Сага, поддерживающая компенсационные действия (откат) в случае сбоя.
    /// </summary>
    public interface ICompensatingSaga : ISaga
    {
        /// <summary>Запустить процесс компенсации (отката).</summary>
        Task Compensate(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Хранилище состояний саг (persistence).
    /// </summary>
    public interface ISagaStateRepository
    {
        Task<ISagaState?> LoadAsync(Guid sagaId, CancellationToken cancellationToken = default);
        Task SaveAsync(ISagaState state, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid sagaId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Диспетчер саг: связывает события с соответствующими экземплярами саг.
    /// </summary>
    public interface ISagaDispatcher
    {
        Task DispatchAsync(IDomainEvent @event, CancellationToken cancellationToken = default);
    }
}