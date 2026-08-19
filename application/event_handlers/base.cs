// application/event_handlers/base.cs
using dnd_game.Domain.Events;

namespace dnd_game.application.event_handlers
{
    /// <summary>
    /// Базовый интерфейс обработчика доменных событий.
    /// Определяет контракт для асинхронной обработки события определённого типа.
    /// </summary>
    /// <typeparam name="TEvent">Тип события, наследующий <see cref="IDomainEvent"/>.</typeparam>
    public interface IEventHandler<in TEvent> where TEvent : IDomainEvent
    {
        /// <summary>
        /// Обрабатывает доменное событие.
        /// </summary>
        /// <param name="event">Экземпляр события для обработки.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Задача, представляющая асинхронную операцию.</returns>
        Task Handle(TEvent @event, CancellationToken cancellationToken);
    }

    // === 1. Интерфейс отправки событий ===
    /// <summary>
    /// Позволяет обработчикам публиковать новые события, что необходимо для
    /// моделирования цепных реакций, например:
    /// - атака огненным шаром вызывает проверки спасбросков у всех целей;
    /// - смерть персонажа вызывает проверку морали у союзников.
    /// </summary>
    public interface IEventPublisher
    {
        /// <summary>
        /// Публикует событие для обработки всеми зарегистрированными подписчиками.
        /// </summary>
        /// <typeparam name="TEvent">Тип события, наследующий <see cref="IDomainEvent"/>.</typeparam>
        /// <param name="event">Публикуемое событие.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Задача, представляющая асинхронную операцию публикации.</returns>
        Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
            where TEvent : IDomainEvent;
    }

    // === 2. Интерфейс саги / процесс-менеджера ===
    /// <summary>
    /// Интерфейс саги (процесс-менеджера) для координации длительных бизнес-процессов.
    /// В контексте DnD используется для отслеживания таких процессов, как бой, отдых,
    /// подготовка заклинаний и других многошаговых сценариев.
    /// </summary>
    /// <typeparam name="TState">Тип состояния саги (должен быть ссылочным типом).</typeparam>
    public interface ISaga<TState> where TState : class
    {
        /// <summary>
        /// Текущее состояние саги.
        /// </summary>
        TState State { get; }

        /// <summary>
        /// Выполняет переход состояния саги на основе произошедшего доменного события.
        /// </summary>
        /// <param name="event">Событие, влияющее на сагу.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Задача, представляющая асинхронную операцию перехода.</returns>
        Task TransitionAsync(IDomainEvent @event, CancellationToken cancellationToken);
    }

    // === 3. Базовый класс обработчика событий ===
    /// <summary>
    /// Базовый класс для обработчиков событий, предоставляющий общую функциональность:
    /// доступ к издателю событий и виртуальные проверки состояния игры и персонажа.
    /// </summary>
    /// <remarks>
    /// Инициализирует новый экземпляр <see cref="EventHandlerBase"/>.
    /// </remarks>
    /// <param name="publisher">Издатель событий для публикации реакций обработчика.</param>
    public abstract class EventHandlerBase(IEventPublisher publisher)
    {
        /// <summary>
        /// Издатель событий, используемый для публикации новых событий из обработчиков.
        /// </summary>
        protected readonly IEventPublisher _publisher = publisher;

        /// <summary>
        /// Виртуальный метод проверки, что персонаж с указанным идентификатором существует и жив.
        /// В реальной реализации должен обращаться к read-модели или хранилищу событий.
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <returns>True, если персонаж жив и может участвовать в событиях; иначе False.</returns>
        protected virtual async Task<bool> IsCharacterAliveAsync(Guid characterId)
        {
            // Заглушка: всегда возвращает true, пока не реализована проверка.
            return await Task.FromResult(true);
        }

        /// <summary>
        /// Виртуальный метод проверки, что игра находится в допустимом состоянии
        /// (например, не в меню настройки или паузе).
        /// </summary>
        /// <returns>True, если игра активна и события должны обрабатываться; иначе False.</returns>
        protected virtual async Task<bool> IsGameActiveAsync()
        {
            return await Task.FromResult(true);
        }
    }

    // === 4. Специализированный обработчик с проверкой состояния персонажа ===
    /// <summary>
    /// Абстрактный обработчик событий, автоматически проверяющий, что персонаж,
    /// связанный с событием, жив, прежде чем вызвать основную логику обработки.
    /// Подходит для событий, которые не должны применяться к мёртвым или недееспособным персонажам.
    /// </summary>
    /// <typeparam name="TEvent">Тип события, наследующий <see cref="IDomainEvent"/>.</typeparam>
    /// <remarks>
    /// Инициализирует новый экземпляр <see cref="CharacterEventhandlerBase{TEvent}"/>.
    /// </remarks>
    /// <param name="publisher">Издатель событий для публикации реакций обработчика.</param>
    public abstract class CharacterEventhandlerBase<TEvent>(IEventPublisher publisher) : EventHandlerBase(publisher),
                                                              IEventHandler<TEvent>
        where TEvent : IDomainEvent
    {

        /// <summary>
        /// Обрабатывает событие, предварительно проверяя жизнеспособность персонажа,
        /// если событие содержит информацию о персонаже (<see cref="ICharacterEvent"/>).
        /// Если персонаж мёртв, обработка пропускается.
        /// </summary>
        /// <param name="event">Событие для обработки.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Задача, представляющая асинхронную операцию.</returns>
        public async Task Handle(TEvent @event, CancellationToken cancellationToken)
        {
            // Проверяем, реализует ли событие интерфейс ICharacterEvent.
            if (@event is ICharacterEvent characterEvent)
            {
                // Если персонаж не жив, игнорируем событие.
                if (!await IsCharacterAliveAsync(characterEvent.CharacterId))
                    return;
            }

            // Вызываем основную логику обработки.
            await HandleCoreAsync(@event, cancellationToken);
        }

        /// <summary>
        /// Основная логика обработки события, которая должна быть реализована в производных классах.
        /// Вызывается только после успешной проверки жизнеспособности персонажа (если применимо).
        /// </summary>
        /// <param name="event">Событие для обработки.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Задача, представляющая асинхронную операцию.</returns>
        protected abstract Task HandleCoreAsync(TEvent @event, CancellationToken cancellationToken);
    }

    // === 5. Интерфейс события, связанного с персонажем ===
    /// <summary>
    /// Интерфейс для доменных событий, содержащих информацию о персонаже.
    /// Позволяет базовым классам автоматически извлекать идентификатор персонажа
    /// для выполнения проверок (например, жизнеспособности).
    /// </summary>
    public interface ICharacterEvent
    {
        /// <summary>
        /// Идентификатор персонажа, связанного с событием.
        /// </summary>
        Guid CharacterId { get; }
    }

    // === 6. Интерфейс для реакций (reactions) ===
    /// <summary>
    /// Интерфейс для событий, которые могут выступать триггером для реакций персонажей.
    /// В DnD персонажи могут реагировать на определённые события вне своего хода
    /// (например, атака при возможности, заклинание Shield).
    /// </summary>
    public interface IReactionEvent : IDomainEvent
    {
        /// <summary>
        /// Описание условия, при котором может быть использована реакция.
        /// Используется для определения соответствия реакции и события.
        /// </summary>
        string ReactionTriggerDescription { get; }
    }

    // === 7. Атрибут для определения порядка обработки событий ===
    /// <summary>
    /// Атрибут, задающий приоритет обработчика событий.
    /// Полезен в сложных механиках, где важен порядок обработки (например,
    /// сначала снижение урона, затем временные хиты, затем обычные).
    /// Может использоваться диспетчером событий для сортировки обработчиков.
    /// </summary>
    /// <remarks>
    /// Инициализирует новый экземпляр <see cref="EventHandlerPriorityAttribute"/>.
    /// </remarks>
    /// <param name="priority">Приоритет обработчика (чем меньше, тем раньше).</param>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class EventHandlerPriorityAttribute(int priority) : Attribute
    {
        /// <summary>
        /// Приоритет обработчика. Меньшее значение соответствует более раннему выполнению.
        /// </summary>
        public int Priority { get; } = priority;
    }
}