// application/event_handlers/ai_handler.cs
using dnd_game.Domain.Events;
using dnd_game.Domain.Commands;
using dnd_game.Infrastructure.MessageBus;
using dnd_game.Application.EventHandlers;

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

        public async Task Handle(CharacterDied @event, CancellationToken cancellationToken)
        {
            // Заглушка: можно автоматически удалять персонажа из боя
            await Task.CompletedTask;
        }

        public async Task Handle(CombatStarted @event, CancellationToken cancellationToken)
        {
            // Заглушка: инициализация AI для участников
            await Task.CompletedTask;
        }

        public async Task Handle(CombatEnded @event, CancellationToken cancellationToken)
        {
            // Заглушка: остановка AI
            await Task.CompletedTask;
        }
    }
}