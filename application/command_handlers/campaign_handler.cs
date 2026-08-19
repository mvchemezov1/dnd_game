using dnd_game.Domain.Aggregates;
using dnd_game.Domain.Commands;
using dnd_game.Domain.Exceptions;
using dnd_game.Infrastructure.EventStore;

namespace dnd_game.application.command_handlers;

/// <summary>
/// Обработчик команд, связанных с управлением квестами в рамках кампании.
/// </summary>
public class CampaignHandler(IEventStore eventStore) : CommandHandlerBase<CampaignAggregate>(eventStore), CommandHandlerBase<CampaignAggregate>,
                               ICommandHandler<AcceptQuestCommand>,
                               ICommandHandler<CompleteQuestCommand>,
                               ICommandHandler<FailQuestCommand>,
                               ICommandHandler<CreateQuestCommand>,
                               ICommandHandler<UpdateQuestObjectiveCommand>
{

    /// <summary>Принимает квест персонажем.</summary>
    public async Task Handle(AcceptQuestCommand command, CancellationToken cancellationToken)
    {
        var aggregate = await LoadAggregate(command.CampaignId, cancellationToken);
        aggregate.AcceptQuest(command.QuestId);
        await SaveAggregate(aggregate, cancellationToken);
    }

    /// <summary>Завершает квест как успешный.</summary>
    public async Task Handle(CompleteQuestCommand command, CancellationToken cancellationToken)
    {
        var aggregate = await LoadAggregate(command.CampaignId, cancellationToken);
        aggregate.CompleteQuest(command.QuestId);
        await SaveAggregate(aggregate, cancellationToken);
    }

    /// <summary>Отмечает квест как проваленный.</summary>
    public async Task Handle(FailQuestCommand command, CancellationToken cancellationToken)
    {
        var aggregate = await LoadAggregate(command.CampaignId, cancellationToken);
        aggregate.FailQuest(command.QuestId);
        await SaveAggregate(aggregate, cancellationToken);
    }

    /// <summary>Создаёт новый квест в кампании.</summary>
    public async Task Handle(CreateQuestCommand command, CancellationToken cancellationToken)
    {
        var aggregate = await LoadAggregate(command.CampaignId, cancellationToken);
        aggregate.CreateQuest(command.QuestId, command.Title, command.Objectives, command.Rewards, command.ParticipantIds);
        await SaveAggregate(aggregate, cancellationToken);
    }

    /// <summary>Обновляет прогресс конкретной цели квеста.</summary>
    public async Task Handle(UpdateQuestObjectiveCommand command, CancellationToken cancellationToken)
    {
        var aggregate = await LoadAggregate(command.CampaignId, cancellationToken);
        aggregate.UpdateQuestObjective(command.QuestId, command.ObjectiveIndex, command.IsCompleted, command.CurrentProgress);
        await SaveAggregate(aggregate, cancellationToken);
    }
}