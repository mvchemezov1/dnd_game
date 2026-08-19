// application/event_handlers/base.cs
using dnd_game.Domain.Events;

namespace dnd_game.Application.EventHandlers
{
    // Исходный интерфейс (оставлен без изменений)
    public interface IEventHandler<in TEvent> where TEvent : IDomainEvent
    {
        Task Handle(TEvent @event, CancellationToken cancellationToken);
    }

    // === 1. Интерфейс отправки событий ===
    // Позволяет обработчикам публиковать новые события, что необходимо для
    // моделирования цепных реакций, например:
    // - атака огненным шаром вызывает проверки спасбросков у всех целей;
    // - смерть персонажа вызывает проверку морали у союзников.
    public interface IEventPublisher
    {
        Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
            where TEvent : IDomainEvent;
    }

    // === 2. Интерфейс саги / процесс-менеджера ===
    // D&D содержит длительные взаимодействия (бой, отдых, подготовка заклинаний).
    // Сага отслеживает корреляцию событий и выдаёт команды, реагируя на изменения.
    public interface ISaga<TState> where TState : class
    {
        TState State { get; }
        Task TransitionAsync(IDomainEvent @event, CancellationToken cancellationToken);
    }

    // === 3. Базовый класс обработчика событий ===
    // Предоставляет общую функциональность: логирование, проверку существования
    // персонажа, разрешение обработки только в определённых фазах игры.
    public abstract class EventHandlerBase
    {
        protected readonly IEventPublisher _publisher;

        protected EventHandlerBase(IEventPublisher publisher)
        {
            _publisher = publisher;
        }

        // Проверка, что персонаж существует и жив (для D&D важна граница смертности)
        protected virtual async Task<bool> IsCharacterAliveAsync(Guid characterId)
        {
            // Реальная реализация должна обращаться к read-модели или event store
            return await Task.FromResult(true);
        }

        // Проверка, что игра находится в допустимом состоянии (например, не в меню)
        protected virtual async Task<bool> IsGameActiveAsync()
        {
            return await Task.FromResult(true);
        }
    }

    // === 4. Специализированный обработчик с проверкой состояния персонажа ===
    // Многие события D&D (получение урона, лечение, использование умений)
    // должны игнорироваться, если персонаж мёртв или без сознания.
    public abstract class CharacterEventhandlerBase<TEvent> : EventHandlerBase,
                                                              IEventHandler<TEvent>
        where TEvent : IDomainEvent
    {
        protected CharacterEventhandlerBase(IEventPublisher publisher)
            : base(publisher) { }

        public async Task Handle(TEvent @event, CancellationToken cancellationToken)
        {
            // Каждое событие, связанное с персонажем, несёт его идентификатор.
            // Определяем его через динамический доступ или явный интерфейс.
            if (@event is ICharacterEvent characterEvent)
            {
                if (!await IsCharacterAliveAsync(characterEvent.CharacterId))
                    return; // Мёртвые не лечатся, не атакуют и т.д.
            }

            await HandleCoreAsync(@event, cancellationToken);
        }

        protected abstract Task HandleCoreAsync(TEvent @event, CancellationToken cancellationToken);
    }

    // === 5. Интерфейс события, связанного с персонажем ===
    // Унифицирует доступ к идентификатору персонажа, позволяя базовому классу
    // автоматически проверять жизнеспособность.
    public interface ICharacterEvent
    {
        Guid CharacterId { get; }
    }

    // === 6. Интерфейс для реакций (reactions) ===
    // В D&D персонажи могут реагировать на определённые триггеры вне своего хода
    // (например, атака при возможности, заклинание Shield). Этот интерфейс
    // помогает обработчикам событий понять, что может быть вызвано в ответ.
    public interface IReactionEvent : IDomainEvent
    {
        // Условие, при котором реакция может быть использована
        string ReactionTriggerDescription { get; }
    }

    // === 7. Атрибут для определения порядка обработки событий ===
    // В сложных механиках важно соблюдать последовательность:
    // сначала применяется снижение урона, потом временные хиты, потом обычные.
    // Этот атрибут (если реализовать диспетчер) позволяет сортировать обработчики.
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class EventHandlerPriorityAttribute : Attribute
    {
        public int Priority { get; }

        public EventHandlerPriorityAttribute(int priority)
        {
            Priority = priority;
        }
    }
}