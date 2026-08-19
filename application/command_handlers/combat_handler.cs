using dnd_game.Domain.Aggregates;
using dnd_game.Domain.Commands;
using dnd_game.Infrastructure.EventStore;
using dnd_game.Domain.Exceptions;

namespace dnd_game.application.command_handlers
{
    /// <summary>
    /// Обрабатывает команды, связанные с боевыми сценами, загружая агрегат <see cref="CombatAggregate"/> из хранилища событий,
    /// вызывая соответствующее поведение домена и сохраняя результирующие события.
    /// Реализует паттерн обработчика команд с использованием событийного сорсинга.
    /// </summary>
    /// <remarks>
    /// Каждый обработчик команды (кроме <see cref="StartCombat"/>, создающего новый агрегат) следует стандартному потоку:
    /// 1. Загрузить агрегат по его идентификатору.
    /// 2. Если агрегат не найден, выбросить исключение <see cref="InvalidAction"/>.
    /// 3. Вызвать метод агрегата, соответствующий команде.
    /// 4. Сохранить агрегат, что приводит к добавлению новых событий в хранилище событий.
    /// </remarks>
    public class CombatHandler(IEventStore eventStore) : ICommandHandler<StartCombat>,
                                 ICommandHandler<EndCombat>,
                                 ICommandHandler<RollInitiative>,
                                 ICommandHandler<StartRound>,
                                 ICommandHandler<NextTurn>,
                                 ICommandHandler<EndRound>,
                                 ICommandHandler<AddParticipantToCombat>,
                                 ICommandHandler<RemoveParticipantFromCombat>,
                                 ICommandHandler<TakeMoveAction>,
                                 ICommandHandler<TakeStandardAction>,
                                 ICommandHandler<TakeBonusAction>,
                                 ICommandHandler<TakeReaction>,
                                 ICommandHandler<ReadyAction>,
                                 ICommandHandler<TriggerReadyAction>,
                                 ICommandHandler<DealDamageToTarget>,
                                 ICommandHandler<HealTarget>,
                                 ICommandHandler<ApplyConditionToTarget>,
                                 ICommandHandler<RemoveConditionFromTarget>,
                                 ICommandHandler<MakeSavingThrowInCombat>,
                                 ICommandHandler<MakeDeathSavingThrowInCombat>,
                                 ICommandHandler<StabilizeInCombat>,
                                 ICommandHandler<MakeConcentrationCheck>,
                                 ICommandHandler<DelayTurn>,
                                 ICommandHandler<SurrenderInCombat>,
                                 ICommandHandler<PerformAction>
    {
        private readonly IEventStore _eventStore = eventStore;

        /// <summary>
        /// Обрабатывает команду <see cref="StartCombat"/>, создавая новую боевую сцену.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя и список участников.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        public async Task Handle(StartCombat command, CancellationToken cancellationToken)
        {
            var aggregate = new CombatAggregate(command.CombatId, command.Participants);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="PerformAction"/>, диспетчеризируя действие участника боя
        /// в зависимости от типа действия и вызывая соответствующий метод агрегата.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя, участника, тип действия, цель и данные действия.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден или тип действия неизвестен.</exception>
        public async Task Handle(PerformAction command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");

            // Диспетчеризация по типу действия
            switch (command.ActionType.ToLowerInvariant())
            {
                case "attack":
                case "standardattack":
                    aggregate.PerformStandardAction(command.ParticipantId, "Attack", command.TargetId, command.ActionData);
                    break;

                case "castspell":
                    // Предполагаем, что ActionData содержит SpellId и SlotLevel
                    // Можно преобразовать в конкретные параметры
                    aggregate.PerformStandardAction(command.ParticipantId, "CastSpell", command.TargetId, command.ActionData);
                    break;

                case "dash":
                    // Действие Dash – это стандартное действие, но можно выделить отдельно
                    aggregate.PerformStandardAction(command.ParticipantId, "Dash", null, null);
                    break;

                case "disengage":
                    aggregate.PerformStandardAction(command.ParticipantId, "Disengage", null, null);
                    break;

                case "dodge":
                    aggregate.PerformStandardAction(command.ParticipantId, "Dodge", null, null);
                    break;

                case "help":
                    aggregate.PerformStandardAction(command.ParticipantId, "Help", command.TargetId, null);
                    break;

                case "hide":
                    aggregate.PerformStandardAction(command.ParticipantId, "Hide", null, null);
                    break;

                case "ready":
                    // Для Ready нужно дополнительно передать условие и действие
                    // Можно расширить ActionData
                    aggregate.ReadyAction(command.ParticipantId, "Ready", command.ActionData?.ToString() ?? "");
                    break;

                case "useobject":
                    aggregate.PerformStandardAction(command.ParticipantId, "UseObject", command.TargetId, command.ActionData);
                    break;

                case "bonus":
                case "bonusaction":
                    // Бонусное действие
                    aggregate.PerformBonusAction(command.ParticipantId, command.ActionType, command.TargetId, command.ActionData);
                    break;

                case "reaction":
                    aggregate.PerformReaction(command.ParticipantId, command.ActionType, command.ActionData?.ToString() ?? "", command.TargetId);
                    break;

                case "move":
                    // Предполагается, что ActionData содержит расстояние в футах
                    int distance = command.ActionData is int d ? d : 0;
                    aggregate.MoveParticipant(command.ParticipantId, distance);
                    break;

                default:
                    throw new InvalidAction($"Unknown action type: {command.ActionType}");
            }

            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="EndCombat"/>, завершая боевую сцену.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(EndCombat command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.EndCombat();
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="RollInitiative"/>, устанавливая инициативу участника.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя, участника, бросок и модификатор ловкости.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(RollInitiative command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.SetParticipantInitiative(command.ParticipantId, command.InitiativeRoll, command.DexterityModifier);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="StartRound"/>, начиная новый раунд боя.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(StartRound command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.StartRound();
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="NextTurn"/>, переходя к следующему ходу участника.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(NextTurn command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.NextTurn();
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="EndRound"/>, завершая текущий раунд боя.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(EndRound command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.EndRound();
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="AddParticipantToCombat"/>, добавляя участника в бой.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя, участника и его инициативу.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(AddParticipantToCombat command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.AddParticipant(command.ParticipantId, command.Initiative);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="RemoveParticipantFromCombat"/>, удаляя участника из боя.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя и участника.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(RemoveParticipantFromCombat command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.RemoveParticipant(command.ParticipantId);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="TakeMoveAction"/>, перемещая участника.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя, участника и дистанцию в футах.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(TakeMoveAction command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.MoveParticipant(command.ParticipantId, command.DistanceFeet);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="TakeStandardAction"/>, выполняя стандартное действие.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя, участника, тип действия, цель и данные.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(TakeStandardAction command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.PerformStandardAction(command.ParticipantId, command.ActionType, command.TargetId, command.ActionData);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="TakeBonusAction"/>, выполняя бонусное действие.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя, участника, тип действия, цель и данные.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(TakeBonusAction command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.PerformBonusAction(command.ParticipantId, command.ActionType, command.TargetId, command.ActionData);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="TakeReaction"/>, выполняя реакцию.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя, участника, тип реакции, описание триггера и цель.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(TakeReaction command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.PerformReaction(command.ParticipantId, command.ReactionType, command.TriggerDescription, command.TargetId);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="ReadyAction"/>, подготавливая действие с условием.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя, участника, действие и условие срабатывания.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(ReadyAction command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.ReadyAction(command.ParticipantId, command.ActionToReady, command.TriggerCondition);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="TriggerReadyAction"/>, активируя подготовленное действие.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя и участника.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(TriggerReadyAction command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.TriggerReadiedAction(command.ParticipantId);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="DealDamageToTarget"/>, нанося урон цели.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя, источник, цель, количество урона и тип урона.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(DealDamageToTarget command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.DealDamage(command.SourceParticipantId, command.TargetParticipantId, command.DamageAmount, command.DamageType);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="HealTarget"/>, восстанавливая здоровье цели.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя, источник, цель и количество лечения.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(HealTarget command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.HealTarget(command.SourceParticipantId, command.TargetParticipantId, command.HealingAmount);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="ApplyConditionToTarget"/>, накладывая состояние на цель.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя, цель, тип состояния и длительность.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(ApplyConditionToTarget command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.ApplyConditionToParticipant(command.TargetParticipantId, command.ConditionType, command.DurationRounds);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="RemoveConditionFromTarget"/>, снимая состояние с цели.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя, цель и тип состояния.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(RemoveConditionFromTarget command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.RemoveConditionFromParticipant(command.TargetParticipantId, command.ConditionType);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="MakeSavingThrowInCombat"/>, выполняя спасбросок участника.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя, участника, характеристику, сложность, бросок и модификаторы.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(MakeSavingThrowInCombat command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.MakeSavingThrow(command.ParticipantId, command.Ability, command.DifficultyClass, command.RollResult, command.Modifiers);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="MakeDeathSavingThrowInCombat"/>, выполняя спасбросок от смерти.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя, участника и результат броска.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(MakeDeathSavingThrowInCombat command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.MakeDeathSavingThrow(command.ParticipantId, command.RollResult);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="StabilizeInCombat"/>, стабилизируя участника.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя, участника и того, кто стабилизирует.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(StabilizeInCombat command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.StabilizeParticipant(command.ParticipantId, command.StabilizedByParticipantId);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="MakeConcentrationCheck"/>, выполняя проверку концентрации.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя, участника, сложность, бросок и модификатор телосложения.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(MakeConcentrationCheck command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.MakeConcentrationCheck(command.ParticipantId, command.DifficultyClass, command.RollResult, command.ConstitutionModifier);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="DelayTurn"/>, откладывая ход участника.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя и участника.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(DelayTurn command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.DelayTurn(command.ParticipantId);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        /// <summary>
        /// Обрабатывает команду <see cref="SurrenderInCombat"/>, помечая участника как сдавшегося.
        /// </summary>
        /// <param name="command">Команда, содержащая идентификатор боя и участника.</param>
        /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
        /// <exception cref="InvalidAction">Выбрасывается, если бой не найден.</exception>
        public async Task Handle(SurrenderInCombat command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.Surrender(command.ParticipantId);
            await _eventStore.Save(aggregate, cancellationToken);
        }
    }
}