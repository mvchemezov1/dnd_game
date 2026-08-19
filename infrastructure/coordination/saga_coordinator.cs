// infrastructure/coordination/saga_coordinator.cs
using dnd_game.Domain.Commands;
using dnd_game.Domain.Events;
using dnd_game.Domain.Sagas;
using dnd_game.infrastructure.message_bus;
using dnd_game.Infrastructure.AI;         // IBlackboardStore (возможное использование)
using Microsoft.Extensions.Logging;

namespace dnd_game.Infrastructure.Coordination
{
    /// <summary>
    /// Реестр фабрик саг. Связывает тип доменного события с функцией, создающей новый экземпляр саги.
    /// </summary>
    public interface ISagaRegistry
    {
        /// <summary>
        /// Зарегистрировать фабрику саги для указанного типа события.
        /// </summary>
        void Register<TEvent>(Func<TEvent, ISaga> factory) where TEvent : IDomainEvent;

        /// <summary>
        /// Получить все фабрики, которые должны реагировать на данный тип события.
        /// </summary>
        IEnumerable<Func<IDomainEvent, ISaga>> GetFactoriesForEvent(Type eventType);
    }

    /// <summary>
    /// Реализация реестра саг на основе словаря.
    /// </summary>
    public class SagaRegistry : ISagaRegistry
    {
        private readonly Dictionary<Type, List<Func<IDomainEvent, ISaga>>> _factories = new();

        public void Register<TEvent>(Func<TEvent, ISaga> factory) where TEvent : IDomainEvent
        {
            if (!_factories.ContainsKey(typeof(TEvent)))
                _factories[typeof(TEvent)] = new List<Func<IDomainEvent, ISaga>>();
            _factories[typeof(TEvent)].Add(e => factory((TEvent)e));
        }

        public IEnumerable<Func<IDomainEvent, ISaga>> GetFactoriesForEvent(Type eventType)
        {
            if (_factories.TryGetValue(eventType, out var list))
                return list;
            return Enumerable.Empty<Func<IDomainEvent, ISaga>>();
        }
    }

    /// <summary>
    /// Координатор саг, реализующий интерфейс ISagaDispatcher.
    /// При получении события находит соответствующие саги (существующие или новые),
    /// загружает/создаёт их состояние, обрабатывает событие и сохраняет изменения.
    /// </summary>
    public class SagaCoordinator : ISagaDispatcher
    {
        private readonly ISagaRegistry _registry;
        private readonly ISagaStateRepository _stateRepository;
        private readonly ICommandBus _commandBus;
        private readonly IDistributedLockManager _lockManager;
        private readonly ILogger<SagaCoordinator> _logger;

        public SagaCoordinator(
            ISagaRegistry registry,
            ISagaStateRepository stateRepository,
            ICommandBus commandBus,
            IDistributedLockManager lockManager,
            ILogger<SagaCoordinator> logger)
        {
            _registry = registry;
            _stateRepository = stateRepository;
            _commandBus = commandBus;
            _lockManager = lockManager;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task DispatchAsync(IDomainEvent @event, CancellationToken cancellationToken = default)
        {
            var factories = _registry.GetFactoriesForEvent(@event.GetType());
            foreach (var factory in factories)
            {
                var saga = factory(@event);
                // Если сага реализует ICommandingSaga, передаём ей CommandBus
                if (saga is ICommandingSaga commandingSaga)
                {
                    commandingSaga.SetCommandBus(_commandBus); // предполагаем, что интерфейс ICommandingSaga имеет такой метод
                }

                // Загружаем существующее состояние, если сага уже существует (по SagaId или CorrelationId)
                ISagaState? state = await _stateRepository.LoadAsync(saga.SagaId, cancellationToken);
                if (state != null)
                {
                    saga.LoadState(state);
                }

                // Блокировка по correlationId (используем SagaId как ключ ресурса) для предотвращения параллельной обработки
                string lockKey = LockKeyFactory.ForSaga(saga.SagaId);  // ← этой строки не хватает
                using var lockHandle = await _lockManager.AcquireAsync(lockKey, LockMode.Exclusive, "coordinator", TimeSpan.FromSeconds(10), cancellationToken);
                if (lockHandle == null)
                {
                    _logger.LogWarning("Could not acquire lock for saga {SagaId}, skipping event.", saga.SagaId);
                    continue;
                }

                try
                {
                    await saga.Handle(@event, cancellationToken);
                    await _stateRepository.SaveAsync(saga.State, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing saga {SagaId} for event {EventType}", saga.SagaId, @event.GetType().Name);
                    // Если сага поддерживает компенсацию, запускаем её
                    if (saga is ICompensatingSaga compensatingSaga)
                    {
                        _logger.LogInformation("Starting compensation for saga {SagaId}", saga.SagaId);
                        await compensatingSaga.Compensate(cancellationToken);
                        saga.State.Status = SagaStatus.Compensating;
                        await _stateRepository.SaveAsync(saga.State, cancellationToken);
                    }
                    else
                    {
                        saga.State.Status = SagaStatus.Failed;
                        await _stateRepository.SaveAsync(saga.State, cancellationToken);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Расширение для ICommandingSaga, позволяющее установить ICommandBus.
    /// </summary>
    public static class CommandingSagaExtensions
    {
        public static void SetCommandBus(this ICommandingSaga saga, ICommandBus commandBus)
        {
            // Реализация через рефлексию или явный метод. Для простоты предполагаем, что интерфейс имеет метод:
            // void SetCommandBus(ICommandBus commandBus);
            // Если нет, можно использовать свойство. В примере мы добавим метод в интерфейс ICommandingSaga.
            if (saga is ICommandBusAware aware)
                aware.CommandBus = commandBus;
        }
    }

    /// <summary>
    /// Интерфейс для саг, которые нуждаются в CommandBus.
    /// </summary>
    public interface ICommandBusAware
    {
        ICommandBus CommandBus { get; set; }
    }
}