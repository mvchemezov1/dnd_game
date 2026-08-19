// application/event_handlers/ai_handler.cs
using dnd_game.Domain.Events;
using dnd_game.Domain.Commands;
using dnd_game.Infrastructure.MessageBus;

namespace dnd_game.application.event_handlers
{
    /// <summary>
    /// Заглушка AI-обработчика. Реагирует только на уже объявленные события.
    /// Для добавления новых реакций необходимо сначала объявить соответствующие события в Domain.Events.
    /// </summary>
    public class AiHandler : IEventHandler<CharacterDied>,
                             IEventHandler<CombatStarted>,
                             IEventHandler<CombatEnded>
    {
        /// <summary>
        /// Обрабатывает событие <see cref="CharacterDied"/>. В текущей реализации является заглушкой и не выполняет действий.
        /// </summary>
        /// <param name="event">Событие смерти персонажа.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <remarks>
        /// Предполагаемая логика: автоматическое удаление персонажа из боя.
        /// </remarks>
        public async Task Handle(CharacterDied @event, CancellationToken cancellationToken)
        {
            // Заглушка: можно автоматически удалять персонажа из боя
            await Task.CompletedTask;
        }

        /// <summary>
        /// Обрабатывает событие <see cref="CombatStarted"/>. В текущей реализации является заглушкой и не выполняет действий.
        /// </summary>
        /// <param name="event">Событие начала боя.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <remarks>
        /// Предполагаемая логика: инициализация AI для участников боя.
        /// </remarks>
        public async Task Handle(CombatStarted @event, CancellationToken cancellationToken)
        {
            // Заглушка: инициализация AI для участников
            await Task.CompletedTask;
        }

        /// <summary>
        /// Обрабатывает событие <see cref="CombatEnded"/>. В текущей реализации является заглушкой и не выполняет действий.
        /// </summary>
        /// <param name="event">Событие окончания боя.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <remarks>
        /// Предполагаемая логика: остановка AI и очистка ресурсов.
        /// </remarks>
        public async Task Handle(CombatEnded @event, CancellationToken cancellationToken)
        {
            // Заглушка: остановка AI
            await Task.CompletedTask;
        }
    }
}