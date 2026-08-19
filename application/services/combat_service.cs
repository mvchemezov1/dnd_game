// application/services/combat_service.cs
using dnd_game.Domain.Commands;
using dnd_game.infrastructure.message_bus;

namespace dnd_game.Application.Services
{
    /// <summary>
    /// Сервис управления боевыми действиями.
    /// Предоставляет высокоуровневый API для отправки команд боевой сцены в шину команд.
    /// Не содержит бизнес-логики — все проверки и изменения состояния выполняются
    /// обработчиками команд и агрегатом <c>CombatAggregate</c>.
    /// </summary>
    /// <remarks>
    /// Паттерн: Application Service (фасад над командной шиной).
    /// Используется для упрощения вызовов со стороны контроллеров и других сервисов.
    /// </remarks>
    public class CombatService(ICommandBus commandBus)
    {
        // ---------- Управление боем ----------

        /// <summary>
        /// Запускает новую боевую сцену с указанными участниками.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <param name="participants">Список идентификаторов участников (персонажей/NPC).</param>
        public async Task StartCombat(Guid combatId, List<Guid> participants)
        {
            await commandBus.SendAsync(new StartCombat(combatId, participants));
        }

        /// <summary>
        /// Завершает активную боевую сцену.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        public async Task EndCombat(Guid combatId)
        {
            await commandBus.SendAsync(new EndCombat(combatId));
        }

        // ---------- Инициатива и раунды ----------

        /// <summary>
        /// Устанавливает инициативу участника боя на основе броска и модификатора ловкости.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <param name="participantId">Идентификатор участника.</param>
        /// <param name="initiativeRoll">Результат броска d20.</param>
        /// <param name="dexterityModifier">Модификатор ловкости участника.</param>
        public async Task RollInitiative(Guid combatId, Guid participantId, int initiativeRoll, int dexterityModifier)
        {
            await commandBus.SendAsync(new RollInitiative(combatId, participantId, initiativeRoll, dexterityModifier));
        }

        /// <summary>
        /// Начинает новый раунд боя.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        public async Task StartRound(Guid combatId)
        {
            await commandBus.SendAsync(new StartRound(combatId));
        }

        /// <summary>
        /// Передаёт ход следующему участнику.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        public async Task NextTurn(Guid combatId)
        {
            await commandBus.SendAsync(new NextTurn(combatId));
        }

        /// <summary>
        /// Завершает текущий раунд боя.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        public async Task EndRound(Guid combatId)
        {
            await commandBus.SendAsync(new EndRound(combatId));
        }

        // ---------- Участники ----------

        /// <summary>
        /// Добавляет нового участника в активный бой.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <param name="participantId">Идентификатор добавляемого участника.</param>
        /// <param name="initiative">Значение инициативы участника.</param>
        public async Task AddParticipantToCombat(Guid combatId, Guid participantId, int initiative)
        {
            await commandBus.SendAsync(new AddParticipantToCombat(combatId, participantId, initiative));
        }

        /// <summary>
        /// Удаляет участника из боя.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <param name="participantId">Идентификатор удаляемого участника.</param>
        public async Task RemoveParticipantFromCombat(Guid combatId, Guid participantId)
        {
            await commandBus.SendAsync(new RemoveParticipantFromCombat(combatId, participantId));
        }

        // ---------- Действия ----------

        /// <summary>
        /// Перемещает участника на указанное расстояние.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <param name="participantId">Идентификатор участника.</param>
        /// <param name="distanceFeet">Дистанция перемещения в футах.</param>
        public async Task TakeMoveAction(Guid combatId, Guid participantId, int distanceFeet)
        {
            await commandBus.SendAsync(new TakeMoveAction(combatId, participantId, distanceFeet));
        }

        /// <summary>
        /// Выполняет стандартное действие участника.
        /// Возможные типы: Attack, CastSpell, Dash, Disengage, Dodge, Help, Hide, Ready, Search, UseObject.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <param name="participantId">Идентификатор участника.</param>
        /// <param name="actionType">Тип действия.</param>
        /// <param name="targetId">Идентификатор цели (если применимо).</param>
        /// <param name="actionData">Дополнительные данные действия (например, параметры заклинания).</param>
        public async Task TakeStandardAction(Guid combatId, Guid participantId, string actionType, Guid? targetId, object? actionData)
        {
            await commandBus.SendAsync(new TakeStandardAction(combatId, participantId, actionType, targetId, actionData));
        }

        /// <summary>
        /// Выполняет бонусное действие участника.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <param name="participantId">Идентификатор участника.</param>
        /// <param name="actionType">Тип бонусного действия.</param>
        /// <param name="targetId">Идентификатор цели (если применимо).</param>
        /// <param name="actionData">Дополнительные данные действия.</param>
        public async Task TakeBonusAction(Guid combatId, Guid participantId, string actionType, Guid? targetId, object? actionData)
        {
            await commandBus.SendAsync(new TakeBonusAction(combatId, participantId, actionType, targetId, actionData));
        }

        /// <summary>
        /// Выполняет реакцию участника (например, провоцированная атака, заклинание Shield).
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <param name="participantId">Идентификатор участника.</param>
        /// <param name="reactionType">Тип реакции.</param>
        /// <param name="triggerDescription">Описание триггера, вызвавшего реакцию.</param>
        /// <param name="targetId">Идентификатор цели (если применимо).</param>
        public async Task TakeReaction(Guid combatId, Guid participantId, string reactionType, string triggerDescription, Guid? targetId)
        {
            await commandBus.SendAsync(new TakeReaction(combatId, participantId, reactionType, triggerDescription, targetId));
        }

        // ---------- Готовое действие ----------

        /// <summary>
        /// Подготавливает действие с условием срабатывания (Ready Action).
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <param name="participantId">Идентификатор участника.</param>
        /// <param name="actionToReady">Подготавливаемое действие.</param>
        /// <param name="triggerCondition">Условие, при котором действие будет выполнено.</param>
        public async Task ReadyAction(Guid combatId, Guid participantId, string actionToReady, string triggerCondition)
        {
            await commandBus.SendAsync(new ReadyAction(combatId, participantId, actionToReady, triggerCondition));
        }

        /// <summary>
        /// Активирует подготовленное действие участника.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <param name="participantId">Идентификатор участника.</param>
        public async Task TriggerReadyAction(Guid combatId, Guid participantId)
        {
            await commandBus.SendAsync(new TriggerReadyAction(combatId, participantId));
        }

        // ---------- Урон и лечение ----------

        /// <summary>
        /// Наносит урон цели от имени источника.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <param name="sourceParticipantId">Идентификатор источника урона.</param>
        /// <param name="targetParticipantId">Идентификатор цели.</param>
        /// <param name="damageAmount">Количество урона.</param>
        /// <param name="damageType">Тип урона (например, "огненный", "дробящий").</param>
        public async Task DealDamageToTarget(Guid combatId, Guid sourceParticipantId, Guid targetParticipantId, int damageAmount, string damageType)
        {
            await commandBus.SendAsync(new DealDamageToTarget(combatId, sourceParticipantId, targetParticipantId, damageAmount, damageType));
        }

        /// <summary>
        /// Восстанавливает здоровье цели от имени источника.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <param name="sourceParticipantId">Идентификатор источника лечения.</param>
        /// <param name="targetParticipantId">Идентификатор цели.</param>
        /// <param name="healingAmount">Количество восстанавливаемых хитов.</param>
        public async Task HealTarget(Guid combatId, Guid sourceParticipantId, Guid targetParticipantId, int healingAmount)
        {
            await commandBus.SendAsync(new HealTarget(combatId, sourceParticipantId, targetParticipantId, healingAmount));
        }

        // ---------- Состояния ----------

        /// <summary>
        /// Накладывает состояние на цель.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <param name="targetParticipantId">Идентификатор цели.</param>
        /// <param name="conditionType">Тип состояния (например, "оглушён", "ослеплён").</param>
        /// <param name="durationRounds">Длительность состояния в раундах.</param>
        public async Task ApplyConditionToTarget(Guid combatId, Guid targetParticipantId, string conditionType, int durationRounds)
        {
            await commandBus.SendAsync(new ApplyConditionToTarget(combatId, targetParticipantId, conditionType, durationRounds));
        }

        /// <summary>
        /// Снимает состояние с цели.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <param name="targetParticipantId">Идентификатор цели.</param>
        /// <param name="conditionType">Тип снимаемого состояния.</param>
        public async Task RemoveConditionFromTarget(Guid combatId, Guid targetParticipantId, string conditionType)
        {
            await commandBus.SendAsync(new RemoveConditionFromTarget(combatId, targetParticipantId, conditionType));
        }

        // ---------- Спасброски ----------

        /// <summary>
        /// Выполняет спасбросок участника в бою.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <param name="participantId">Идентификатор участника.</param>
        /// <param name="ability">Характеристика для спасброска (например, "Ловкость").</param>
        /// <param name="difficultyClass">Сложность спасброска.</param>
        /// <param name="rollResult">Результат броска d20.</param>
        /// <param name="modifiers">Суммарные модификаторы.</param>
        public async Task MakeSavingThrow(Guid combatId, Guid participantId, string ability, int difficultyClass, int rollResult, int modifiers)
        {
            await commandBus.SendAsync(new MakeSavingThrowInCombat(combatId, participantId, ability, difficultyClass, rollResult, modifiers));
        }

        /// <summary>
        /// Выполняет спасбросок от смерти.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <param name="participantId">Идентификатор участника.</param>
        /// <param name="rollResult">Результат броска d20.</param>
        public async Task MakeDeathSavingThrow(Guid combatId, Guid participantId, int rollResult)
        {
            await commandBus.SendAsync(new MakeDeathSavingThrowInCombat(combatId, participantId, rollResult));
        }

        /// <summary>
        /// Стабилизирует участника, находящегося при смерти.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <param name="participantId">Идентификатор стабилизируемого участника.</param>
        /// <param name="stabilizedByParticipantId">Идентификатор участника, оказавшего помощь.</param>
        public async Task StabilizeInCombat(Guid combatId, Guid participantId, Guid stabilizedByParticipantId)
        {
            await commandBus.SendAsync(new StabilizeInCombat(combatId, participantId, stabilizedByParticipantId));
        }

        // ---------- Концентрация ----------

        /// <summary>
        /// Выполняет проверку концентрации (обычно после получения урона).
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <param name="participantId">Идентификатор участника.</param>
        /// <param name="difficultyClass">Сложность проверки (обычно 10 или половина урона).</param>
        /// <param name="rollResult">Результат броска d20.</param>
        /// <param name="constitutionModifier">Модификатор телосложения.</param>
        public async Task MakeConcentrationCheck(Guid combatId, Guid participantId, int difficultyClass, int rollResult, int constitutionModifier)
        {
            await commandBus.SendAsync(new MakeConcentrationCheck(combatId, participantId, difficultyClass, rollResult, constitutionModifier));
        }

        // ---------- Прочие действия ----------

        /// <summary>
        /// Откладывает ход участника (Delay).
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <param name="participantId">Идентификатор участника.</param>
        public async Task DelayTurn(Guid combatId, Guid participantId)
        {
            await commandBus.SendAsync(new DelayTurn(combatId, participantId));
        }

        /// <summary>
        /// Помечает участника как сдавшегося.
        /// </summary>
        /// <param name="combatId">Идентификатор боя.</param>
        /// <param name="participantId">Идентификатор участника.</param>
        public async Task SurrenderInCombat(Guid combatId, Guid participantId)
        {
            await commandBus.SendAsync(new SurrenderInCombat(combatId, participantId));
        }
    }
}