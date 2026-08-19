using dnd_game.Domain.Aggregates;
using dnd_game.Domain.Commands;
using dnd_game.Infrastructure.EventStore;
using dnd_game.Domain.Exceptions;

namespace dnd_game.application.command_handlers
{
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

        public async Task Handle(StartCombat command, CancellationToken cancellationToken)
        {
            var aggregate = new CombatAggregate(command.CombatId, command.Participants);
            await _eventStore.Save(aggregate, cancellationToken);
        }

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

        public async Task Handle(EndCombat command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.EndCombat();
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(RollInitiative command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.SetParticipantInitiative(command.ParticipantId, command.InitiativeRoll, command.DexterityModifier);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(StartRound command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.StartRound();
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(NextTurn command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.NextTurn();
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(EndRound command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.EndRound();
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(AddParticipantToCombat command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.AddParticipant(command.ParticipantId, command.Initiative);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(RemoveParticipantFromCombat command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.RemoveParticipant(command.ParticipantId);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(TakeMoveAction command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.MoveParticipant(command.ParticipantId, command.DistanceFeet);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(TakeStandardAction command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.PerformStandardAction(command.ParticipantId, command.ActionType, command.TargetId, command.ActionData);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(TakeBonusAction command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.PerformBonusAction(command.ParticipantId, command.ActionType, command.TargetId, command.ActionData);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(TakeReaction command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.PerformReaction(command.ParticipantId, command.ReactionType, command.TriggerDescription, command.TargetId);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(ReadyAction command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.ReadyAction(command.ParticipantId, command.ActionToReady, command.TriggerCondition);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(TriggerReadyAction command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.TriggerReadiedAction(command.ParticipantId);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(DealDamageToTarget command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.DealDamage(command.SourceParticipantId, command.TargetParticipantId, command.DamageAmount, command.DamageType);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(HealTarget command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.HealTarget(command.SourceParticipantId, command.TargetParticipantId, command.HealingAmount);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(ApplyConditionToTarget command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.ApplyConditionToParticipant(command.TargetParticipantId, command.ConditionType, command.DurationRounds);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(RemoveConditionFromTarget command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.RemoveConditionFromParticipant(command.TargetParticipantId, command.ConditionType);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(MakeSavingThrowInCombat command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.MakeSavingThrow(command.ParticipantId, command.Ability, command.DifficultyClass, command.RollResult, command.Modifiers);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(MakeDeathSavingThrowInCombat command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.MakeDeathSavingThrow(command.ParticipantId, command.RollResult);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(StabilizeInCombat command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.StabilizeParticipant(command.ParticipantId, command.StabilizedByParticipantId);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(MakeConcentrationCheck command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.MakeConcentrationCheck(command.ParticipantId, command.DifficultyClass, command.RollResult, command.ConstitutionModifier);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(DelayTurn command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.DelayTurn(command.ParticipantId);
            await _eventStore.Save(aggregate, cancellationToken);
        }

        public async Task Handle(SurrenderInCombat command, CancellationToken cancellationToken)
        {
            var aggregate = await _eventStore.Load<CombatAggregate>(command.CombatId, cancellationToken)
                            ?? throw new InvalidAction("Combat not found");
            aggregate.Surrender(command.ParticipantId);
            await _eventStore.Save(aggregate, cancellationToken);
        }
    }
}