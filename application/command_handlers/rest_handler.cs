// application/command_handlers/rest_handler.cs
using dnd_game.Domain.Commands;
using dnd_game.Domain.Aggregates;
using dnd_game.Infrastructure.EventStore;

namespace dnd_game.Application.CommandHandlers;

public class RestHandler : ICommandHandler<StartRest>,
                             ICommandHandler<EndRest>,
                             ICommandHandler<SpendHitDie>,
                             ICommandHandler<InterruptRest>
{
    private readonly IEventStore _eventStore;

    public RestHandler(IEventStore eventStore) => _eventStore = eventStore;

    public async Task Handle(StartRest command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, CancellationToken.None)
                        ?? throw new Domain.Exceptions.InvalidAction("Character not found");
        aggregate.StartRest(command.RestType);
        await _eventStore.Save(aggregate, CancellationToken.None);
    }

    public async Task Handle(SpendHitDie command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, CancellationToken.None)
                        ?? throw new Domain.Exceptions.InvalidAction("Character not found");
        aggregate.SpendHitDie(command.HitDieType, command.Roll, command.ConstitutionModifier);
        await _eventStore.Save(aggregate, CancellationToken.None);
    }

    public async Task Handle(InterruptRest command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, CancellationToken.None)
                        ?? throw new Domain.Exceptions.InvalidAction("Character not found");
        aggregate.InterruptRest(command.InterruptionType);
        await _eventStore.Save(aggregate, CancellationToken.None);
    }

    public async Task Handle(EndRest command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, CancellationToken.None)
                        ?? throw new Domain.Exceptions.InvalidAction("Character not found");
        aggregate.EndRest();
        await _eventStore.Save(aggregate, CancellationToken.None);
    }
}