using dnd_game.Domain.Aggregates;
using dnd_game.Domain.Commands;
using dnd_game.Domain.Exceptions;
using dnd_game.Infrastructure.EventStore;

namespace dnd_game.application.command_handlers
{
    /// <summary>
    /// Обрабатывает команды, связанные с кампанией, загружая агрегат <see cref="CampaignAggregate"/> из хранилища событий,
    /// вызывая соответствующее поведение домена и сохраняя результирующие события.
    /// Реализует паттерн обработчика команд с использованием событийного сорсинга.
    /// </summary>
    /// <remarks>
    /// Каждый обработчик команды следует одному и тому же потоку:
    /// 1. Загрузить агрегат по его идентификатору.
    /// 2. Если агрегат не найден, выбросить исключение <see cref="InvalidAction"/>.
    /// 3. Вызвать метод агрегата, соответствующий команде.
    /// 4. Сохранить агрегат, что приводит к добавлению новых событий в хранилище событий.
    /// </remarks>
    public class CampaignHandler(IEventStore _eventStore) : ICommandHandler<AcceptQuestCommand>,
                                                           ICommandHandler<CompleteQuestCommand>,
                                                           ICommandHandler<FailQuestCommand>,
                                                           ICommandHandler<CreateQuestCommand>,
                                                           ICommandHandler<UpdateQuestObjectiveCommand>
    {
        /// <summary>
        /// Обрабатывает команду <see cref="AcceptQuestCommand"/>, загружая агрегат кампании
        /// и помечая указанное задание как принятое.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор кампании и идентификатор задания.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если агрегат кампании не существует.</exception>
        public async Task Handle(AcceptQuestCommand command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CampaignAggregate>(command.CampaignId, cancellationToken)
                            ?? throw new InvalidAction("Campaign not found");
            aggregate.AcceptQuest(command.QuestId);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="CompleteQuestCommand"/>, загружая агрегат кампании
        /// и помечая указанное задание как выполненное.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор кампании и идентификатор задания.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если агрегат кампании не существует.</exception>
        public async Task Handle(CompleteQuestCommand command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CampaignAggregate>(command.CampaignId, cancellationToken)
                            ?? throw new InvalidAction("Campaign not found");
            aggregate.CompleteQuest(command.QuestId);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="FailQuestCommand"/>, загружая агрегат кампании
        /// и помечая указанное задание как проваленное.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор кампании и идентификатор задания.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если агрегат кампании не существует.</exception>
        public async Task Handle(FailQuestCommand command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CampaignAggregate>(command.CampaignId, cancellationToken)
                            ?? throw new InvalidAction("Campaign not found");
            aggregate.FailQuest(command.QuestId);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="CreateQuestCommand"/>, загружая агрегат кампании
        /// и создавая новое задание с предоставленными данными.
        /// </summary>
        /// <param name="command">Команда, содержащая все данные для создания задания.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если агрегат кампании не существует.</exception>
        public async Task Handle(CreateQuestCommand command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CampaignAggregate>(command.CampaignId, cancellationToken)
                            ?? throw new InvalidAction("Campaign not found");

            aggregate.CreateQuest(
                command.QuestId,
                command.Title,
                command.Objectives,
                command.Rewards,
                command.ParticipantIds
            );

            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="UpdateQuestObjectiveCommand"/>, загружая агрегат кампании
        /// и обновляя прогресс конкретной цели задания.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор задания, индекс цели, флаг завершения и прогресс.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если агрегат кампании не существует.</exception>
        public async Task Handle(UpdateQuestObjectiveCommand command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CampaignAggregate>(command.CampaignId, cancellationToken)
                            ?? throw new InvalidAction("Campaign not found");

            aggregate.UpdateQuestObjective(
                command.QuestId,
                command.ObjectiveIndex,
                command.IsCompleted,
                command.CurrentProgress
            );

            await _eventStore.Save(aggregate, cancellationToken);
        }
    }
}