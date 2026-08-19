using dnd_game.Domain.Aggregates;
using dnd_game.Domain.Commands;
using dnd_game.Domain.Exceptions;
using dnd_game.Infrastructure.EventStore;

namespace dnd_game.application.command_handlers
{
    public class CampaignHandler(IEventStore _eventStore) : ICommandHandler<AcceptQuestCommand>,
                                                           ICommandHandler<CompleteQuestCommand>,
                                                           ICommandHandler<FailQuestCommand>,
                                                           ICommandHandler<CreateQuestCommand>,
                                                           ICommandHandler<UpdateQuestObjectiveCommand>
    {
        public async Task Handle(AcceptQuestCommand command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CampaignAggregate>(command.CampaignId, cancellationToken)
                            ?? throw new InvalidAction("Campaign not found");
            aggregate.AcceptQuest(command.QuestId);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(CompleteQuestCommand command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CampaignAggregate>(command.CampaignId, cancellationToken)
                            ?? throw new InvalidAction("Campaign not found");
            aggregate.CompleteQuest(command.QuestId);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(FailQuestCommand command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CampaignAggregate>(command.CampaignId, cancellationToken)
                            ?? throw new InvalidAction("Campaign not found");
            aggregate.FailQuest(command.QuestId);
            await _eventStore.Save(aggregate, cancellationToken);
        }

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
