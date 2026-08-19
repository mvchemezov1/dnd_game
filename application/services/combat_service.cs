// application/services/combat_service.cs
using dnd_game.Domain.Commands;
using dnd_game.infrastructure.message_bus;

namespace dnd_game.Application.Services
{
    /// <summary>
    /// Сервис для управления боевыми действиями.
    /// Отправляет команды в шину команд, не содержит бизнес‑логики.
    /// </summary>
    public class CombatService(ICommandBus commandBus)
    {

        // ---------- Управление боем ----------
        public async Task StartCombat(Guid combatId, List<Guid> participants)
        {
            await commandBus.SendAsync(new StartCombat(combatId, participants));
        }

        public async Task EndCombat(Guid combatId)
        {
            await commandBus.SendAsync(new EndCombat(combatId));
        }

        // ---------- Инициатива и раунды ----------
        public async Task RollInitiative(Guid combatId, Guid participantId, int initiativeRoll, int dexterityModifier)
        {
            await commandBus.SendAsync(new RollInitiative(combatId, participantId, initiativeRoll, dexterityModifier));
        }

        public async Task StartRound(Guid combatId)
        {
            await commandBus.SendAsync(new StartRound(combatId));
        }

        public async Task NextTurn(Guid combatId)
        {
            await commandBus.SendAsync(new NextTurn(combatId));
        }

        public async Task EndRound(Guid combatId)
        {
            await commandBus.SendAsync(new EndRound(combatId));
        }

        // ---------- Участники ----------
        public async Task AddParticipantToCombat(Guid combatId, Guid participantId, int initiative)
        {
            await commandBus.SendAsync(new AddParticipantToCombat(combatId, participantId, initiative));
        }

        public async Task RemoveParticipantFromCombat(Guid combatId, Guid participantId)
        {
            await commandBus.SendAsync(new RemoveParticipantFromCombat(combatId, participantId));
        }

        // ---------- Действия ----------
        public async Task TakeMoveAction(Guid combatId, Guid participantId, int distanceFeet)
        {
            await commandBus.SendAsync(new TakeMoveAction(combatId, participantId, distanceFeet));
        }

        /// <summary>
        /// Стандартное действие: Attack, CastSpell, Dash, Disengage, Dodge, Help, Hide, Ready, Search, UseObject.
        /// </summary>
        public async Task TakeStandardAction(Guid combatId, Guid participantId, string actionType, Guid? targetId, object? actionData)
        {
            await commandBus.SendAsync(new TakeStandardAction(combatId, participantId, actionType, targetId, actionData));
        }

        public async Task TakeBonusAction(Guid combatId, Guid participantId, string actionType, Guid? targetId, object? actionData)
        {
            await commandBus.SendAsync(new TakeBonusAction(combatId, participantId, actionType, targetId, actionData));
        }

        public async Task TakeReaction(Guid combatId, Guid participantId, string reactionType, string triggerDescription, Guid? targetId)
        {
            await commandBus.SendAsync(new TakeReaction(combatId, participantId, reactionType, triggerDescription, targetId));
        }

        // ---------- Готовое действие ----------
        public async Task ReadyAction(Guid combatId, Guid participantId, string actionToReady, string triggerCondition)
        {
            await commandBus.SendAsync(new ReadyAction(combatId, participantId, actionToReady, triggerCondition));
        }

        public async Task TriggerReadyAction(Guid combatId, Guid participantId)
        {
            await commandBus.SendAsync(new TriggerReadyAction(combatId, participantId));
        }

        // ---------- Урон и лечение ----------
        public async Task DealDamageToTarget(Guid combatId, Guid sourceParticipantId, Guid targetParticipantId, int damageAmount, string damageType)
        {
            await commandBus.SendAsync(new DealDamageToTarget(combatId, sourceParticipantId, targetParticipantId, damageAmount, damageType));
        }

        public async Task HealTarget(Guid combatId, Guid sourceParticipantId, Guid targetParticipantId, int healingAmount)
        {
            await commandBus.SendAsync(new HealTarget(combatId, sourceParticipantId, targetParticipantId, healingAmount));
        }

        // ---------- Состояния ----------
        public async Task ApplyConditionToTarget(Guid combatId, Guid targetParticipantId, string conditionType, int durationRounds)
        {
            await commandBus.SendAsync(new ApplyConditionToTarget(combatId, targetParticipantId, conditionType, durationRounds));
        }

        public async Task RemoveConditionFromTarget(Guid combatId, Guid targetParticipantId, string conditionType)
        {
            await commandBus.SendAsync(new RemoveConditionFromTarget(combatId, targetParticipantId, conditionType));
        }

        // ---------- Спасброски ----------
        public async Task MakeSavingThrow(Guid combatId, Guid participantId, string ability, int difficultyClass, int rollResult, int modifiers)
        {
            await commandBus.SendAsync(new MakeSavingThrowInCombat(combatId, participantId, ability, difficultyClass, rollResult, modifiers));
        }

        public async Task MakeDeathSavingThrow(Guid combatId, Guid participantId, int rollResult)
        {
            await commandBus.SendAsync(new MakeDeathSavingThrowInCombat(combatId, participantId, rollResult));
        }

        public async Task StabilizeInCombat(Guid combatId, Guid participantId, Guid stabilizedByParticipantId)
        {
            await commandBus.SendAsync(new StabilizeInCombat(combatId, participantId, stabilizedByParticipantId));
        }

        // ---------- Концентрация ----------
        public async Task MakeConcentrationCheck(Guid combatId, Guid participantId, int difficultyClass, int rollResult, int constitutionModifier)
        {
            await commandBus.SendAsync(new MakeConcentrationCheck(combatId, participantId, difficultyClass, rollResult, constitutionModifier));
        }

        // ---------- Прочие действия ----------
        public async Task DelayTurn(Guid combatId, Guid participantId)
        {
            await commandBus.SendAsync(new DelayTurn(combatId, participantId));
        }

        public async Task SurrenderInCombat(Guid combatId, Guid participantId)
        {
            await commandBus.SendAsync(new SurrenderInCombat(combatId, participantId));
        }
    }
}