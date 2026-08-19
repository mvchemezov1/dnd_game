// application/command_handlers/rest_handler.cs
using dnd_game.Domain.Commands;
using dnd_game.Domain.Aggregates;
using dnd_game.Infrastructure.EventStore;

namespace dnd_game.Application.CommandHandlers;

/// <summary>
/// Обрабатывает команды, связанные с отдыхом персонажа, загружая агрегат <see cref="CharacterAggregate"/> из хранилища событий,
/// вызывая соответствующее поведение домена и сохраняя результирующие события.
/// Реализует паттерн обработчика команд с использованием событийного сорсинга.
/// </summary>
/// <remarks>
/// Каждый обработчик следует стандартному потоку:
/// 1. Загрузить агрегат персонажа по идентификатору.
/// 2. Если персонаж не найден, выбросить исключение <see cref="Domain.Exceptions.InvalidAction"/>.
/// 3. Вызвать метод агрегата, соответствующий команде.
/// 4. Сохранить агрегат, что приводит к добавлению новых событий в хранилище событий.
/// </remarks>
public class RestHandler : ICommandHandler<StartRest>,
                             ICommandHandler<EndRest>,
                             ICommandHandler<SpendHitDie>,
                             ICommandHandler<InterruptRest>
{
    private readonly IEventStore _eventStore;

    public RestHandler(IEventStore eventStore) => _eventStore = eventStore;

    /// <summary>
    /// Обрабатывает команду <see cref="StartRest"/>, начиная отдых персонажа указанного типа.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и тип отдыха (короткий или длинный).</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="Domain.Exceptions.InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(StartRest command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, CancellationToken.None)
                        ?? throw new Domain.Exceptions.InvalidAction("Character not found");
        aggregate.StartRest(command.RestType);
        await _eventStore.Save(aggregate, CancellationToken.None);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="SpendHitDie"/>, расходуя кость хитов персонажа для восстановления здоровья.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа, тип кости хитов, результат броска и модификатор телосложения.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="Domain.Exceptions.InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(SpendHitDie command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, CancellationToken.None)
                        ?? throw new Domain.Exceptions.InvalidAction("Character not found");
        aggregate.SpendHitDie(command.HitDieType, command.Roll, command.ConstitutionModifier);
        await _eventStore.Save(aggregate, CancellationToken.None);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="InterruptRest"/>, прерывая текущий отдых персонажа.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и тип прерывания.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="Domain.Exceptions.InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(InterruptRest command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, CancellationToken.None)
                        ?? throw new Domain.Exceptions.InvalidAction("Character not found");
        aggregate.InterruptRest(command.InterruptionType);
        await _eventStore.Save(aggregate, CancellationToken.None);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="EndRest"/>, завершая отдых персонажа.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="Domain.Exceptions.InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(EndRest command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, CancellationToken.None)
                        ?? throw new Domain.Exceptions.InvalidAction("Character not found");
        aggregate.EndRest();
        await _eventStore.Save(aggregate, CancellationToken.None);
    }
}