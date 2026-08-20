// domain/aggregates/combat_aggregate.cs
using dnd_game.Domain.Events;
using dnd_game.SharedKernel;

namespace dnd_game.Domain.Aggregates;

/// <summary>
/// Агрегат боевой сцены. Управляет участниками, раундами, ходами, действиями и состояниями в бою.
/// Реализует событийно-ориентированное восстановление состояния (event sourcing).
/// </summary>
public class CombatAggregate : AggregateRoot
{
    // ---------- Состояние боя ----------

    /// <summary>Список участников боя с их текущим состоянием.</summary>
    public List<CombatParticipant> Participants { get; private set; } = [];

    /// <summary>Признак активности боевой сцены.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Текущий раунд (0 — если раунд ещё не начался).</summary>
    public int Round { get; private set; } = 0;

    /// <summary>Индекс текущего участника в списке <see cref="Participants"/>. -1, если ход не определён.</summary>
    public int CurrentTurnIndex { get; private set; } = -1;

    // ---------- Конструкторы ----------

    /// <summary>
    /// Создаёт новую боевую сцену с указанными участниками, применяя событие <see cref="CombatStarted"/>.
    /// </summary>
    /// <param name="combatId">Идентификатор боя.</param>
    /// <param name="participantIds">Идентификаторы персонажей-участников.</param>
    public CombatAggregate(Guid combatId, IEnumerable<Guid> participantIds)
    {
        ApplyChange(new CombatStarted(combatId, participantIds.ToList(), DateTime.UtcNow));
    }

    /// <summary>
    /// Конструктор без параметров для восстановления агрегата из истории событий.
    /// </summary>
    public CombatAggregate() { }

    // ---------- Применение событий ----------

    /// <summary>
    /// Применяет доменное событие к состоянию агрегата.
    /// </summary>
    /// <param name="event">Событие предметной области.</param>
    protected override void ApplyEvent(Events.IDomainEvent @event)
    {
        switch (@event)
        {
            // Начало боя: инициализация участников и состояния
            case CombatStarted e:
                Id = e.CombatId;
                Participants = e.Participants.Select(id => new CombatParticipant(id)).ToList();
                IsActive = true;
                Round = 0;
                CurrentTurnIndex = -1;
                break;

            // Завершение боя: бой становится неактивным
            case CombatEnded e:
                IsActive = false;
                break;

            // Бросок инициативы: обновление инициативы и модификатора ловкости участника
            case InitiativeRolled e:
                var participant = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                if (participant != null)
                {
                    participant.Initiative = e.Initiative;
                    participant.DexterityModifier = e.DexterityModifier; // для разрешения ничьих
                }
                // Пересортировка и установка порядка происходит явно при старте раунда
                break;

            // Начало раунда: сортировка участников по инициативе, сброс действий и движения
            case CombatRoundStarted e:
                Round = e.Round;
                CurrentTurnIndex = 0; // первый в порядке инициативы
                // Сортируем участников по инициативе (по убыванию) и сбрасываем действия
                Participants = [.. Participants
                    .OrderByDescending(p => p.Initiative)
                    .ThenByDescending(p => p.DexterityModifier)];
                foreach (var p in Participants)
                {
                    p.IsCurrentTurn = false;
                    p.HasAction = true;
                    p.HasBonusAction = true;
                    p.HasReaction = true; // реакция восстанавливается в начале своего хода (но не всего раунда, см. правила)
                    p.HasMovement = true;
                    p.MovementRemaining = 30; // базовая скорость; в реальной системе загружать из проекции персонажа
                }
                break;

            // Начало хода участника: сброс действий и установка признака текущего хода
            case CombatTurnStarted e:
                var turnParticipant = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                if (turnParticipant != null)
                {
                    turnParticipant.IsCurrentTurn = true;
                    turnParticipant.HasAction = true;
                    turnParticipant.HasBonusAction = true;
                    turnParticipant.HasReaction = true; // реакция восстанавливается в начале СВОЕГО хода
                    turnParticipant.HasMovement = true;
                    turnParticipant.MovementRemaining = 30;
                }
                CurrentTurnIndex = Participants.FindIndex(p => p.CharacterId == e.CharacterId);
                break;

            // Окончание хода участника: снятие признака текущего хода
            case CombatTurnEnded e:
                var endedParticipant = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                if (endedParticipant != null)
                {
                    endedParticipant.IsCurrentTurn = false;
                }
                break;

            // Добавление участника в бой (например, подкрепление)
            case ParticipantAddedToCombat e:
                if (!Participants.Any(p => p.CharacterId == e.CharacterId))
                {
                    Participants.Add(new CombatParticipant(e.CharacterId)
                    {
                        Initiative = e.Initiative
                    });
                }
                break;

            // Удаление участника из боя (смерть, бегство и т.п.)
            case ParticipantRemovedFromCombat e:
                Participants.RemoveAll(p => p.CharacterId == e.CharacterId);
                // Если удалили текущего, нужно переопределить порядок (но лучше оставить как есть, следующий ход будет корректным)
                break;

            // Использование основного действия
            case CombatActionTaken e:
                var actor = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                if (actor != null) actor.HasAction = false;
                break;

            // Использование бонусного действия
            case CombatBonusActionTaken e:
                var bonusActor = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                if (bonusActor != null) bonusActor.HasBonusAction = false;
                break;

            // Использование реакции
            case CombatReactionUsed e:
                var reactor = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                if (reactor != null) reactor.HasReaction = false;
                break;

            // Использование части перемещения
            case CombatMovementUsed e:
                var mover = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                if (mover != null) mover.MovementRemaining = Math.Max(0, mover.MovementRemaining - e.Feet);
                break;

            // Наложение состояния на участника
            case ConditionAppliedToCombatant e:
                var target = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                if (target != null && !target.Conditions.Contains(e.Condition))
                    target.Conditions.Add(e.Condition);
                break;

            // Снятие состояния с участника
            case ConditionRemovedFromCombatant e:
                var condTarget = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                condTarget?.Conditions.Remove(e.Condition);
                break;

            // Начало концентрации
            case CombatConcentrationStarted e:
                var conc = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                if (conc != null) conc.Concentrating = true;
                break;

            // Окончание концентрации
            case CombatConcentrationEnded e:
                var concEnd = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                if (concEnd != null) concEnd.Concentrating = false;
                break;

            // Эти события на состояние агрегата напрямую не влияют,
            // но обрабатываются для полноты switch (фактические эффекты применяются в других агрегатах/проекциях)
            case CombatDamageDealt e: break;
            case CombatHealingDealt e: break;
            case CombatSavingThrowMade e: break;
            case CombatDeathSavingThrowMade e: break;
            case CombatParticipantStabilized e: break;
            case CombatConcentrationCheckMade e: break;
            case CombatTurnDelayed e: break;
            case CombatSurrender e: break;
        }
    }

    // ---------- Команды (методы) ----------

    /// <summary>
    /// Завершает активный бой.
    /// </summary>
    /// <exception cref="InvalidOperationException">Если бой уже неактивен.</exception>
    public void EndCombat()
    {
        if (!IsActive) throw new InvalidOperationException("Combat not active");
        ApplyChange(new CombatEnded(Id, DateTime.UtcNow));
    }

    /// <summary>
    /// Игрок/Мастер бросает инициативу для персонажа.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="initiative">Результат броска инициативы.</param>
    /// <param name="dexterityModifier">Модификатор ловкости (для разрешения ничьих).</param>
    /// <exception cref="InvalidOperationException">Если бой неактивен или персонаж не участвует.</exception>
    public void RollInitiative(Guid characterId, int initiative, int dexterityModifier)
    {
        if (!IsActive) throw new InvalidOperationException("Combat not active");
        if (Participants.All(p => p.CharacterId != characterId))
            throw new InvalidOperationException("Character is not a participant");
        ApplyChange(new InitiativeRolled(Id, characterId, initiative, dexterityModifier));
    }

    /// <summary>
    /// Начать новый раунд (после того, как все бросили инициативу или после окончания предыдущего раунда).
    /// </summary>
    /// <exception cref="InvalidOperationException">Если бой неактивен или не у всех участников определена инициатива.</exception>
    public void StartRound()
    {
        if (!IsActive) throw new InvalidOperationException("Combat not active");
        if (Participants.Any(p => p.Initiative == 0 && p.CharacterId != Guid.Empty))
            throw new InvalidOperationException("Not all participants have rolled initiative.");
        ApplyChange(new CombatRoundStarted(Id, Round + 1, DateTime.UtcNow));
    }

    /// <summary>
    /// Начать ход указанного персонажа.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <exception cref="InvalidOperationException">Если бой неактивен или участник не найден.</exception>
    public void StartTurn(Guid characterId)
    {
        if (!IsActive) throw new InvalidOperationException("Combat not active");
        var participant = Participants.FirstOrDefault(p => p.CharacterId == characterId)
            ?? throw new InvalidOperationException("Participant not found");
        ApplyChange(new CombatTurnStarted(Id, characterId, DateTime.UtcNow));
    }

    /// <summary>
    /// Завершить текущий ход участника.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа, чей ход завершается.</param>
    /// <exception cref="InvalidOperationException">Если бой неактивен, участник не найден или не его ход.</exception>
    public void EndTurn(Guid characterId)
    {
        if (!IsActive) throw new InvalidOperationException("Combat not active");
        var participant = Participants.FirstOrDefault(p => p.CharacterId == characterId)
            ?? throw new InvalidOperationException("Participant not found");
        if (!participant.IsCurrentTurn) throw new InvalidOperationException("Not current turn");
        ApplyChange(new CombatTurnEnded(Id, characterId, DateTime.UtcNow));
    }

    /// <summary>
    /// Добавить участника в бой (например, подкрепление).
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="initiative">Значение инициативы.</param>
    /// <exception cref="InvalidOperationException">Если бой неактивен.</exception>
    public void AddParticipant(Guid characterId, int initiative)
    {
        if (!IsActive) throw new InvalidOperationException("Combat not active");
        ApplyChange(new ParticipantAddedToCombat(Id, characterId, initiative));
    }

    /// <summary>
    /// Удалить участника из боя (смерть/бегство).
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <exception cref="InvalidOperationException">Если бой неактивен или участник не найден.</exception>
    public void RemoveParticipant(Guid characterId)
    {
        if (!IsActive) throw new InvalidOperationException("Combat not active");
        if (!Participants.Any(p => p.CharacterId == characterId))
            throw new InvalidOperationException("Participant not found");
        ApplyChange(new ParticipantRemovedFromCombat(Id, characterId));
    }

    /// <summary>
    /// Использовать основное действие участником.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <exception cref="InvalidOperationException">Если участник не найден или действие недоступно.</exception>
    public void UseAction(Guid characterId)
    {
        var p = Participants.FirstOrDefault(pp => pp.CharacterId == characterId)
            ?? throw new InvalidOperationException("Participant not found");
        if (!p.HasAction) throw new InvalidOperationException("No action available");
        ApplyChange(new CombatActionTaken(Id, characterId));
    }

    /// <summary>
    /// Использовать бонусное действие участником.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <exception cref="InvalidOperationException">Если участник не найден или бонусное действие недоступно.</exception>
    public void UseBonusAction(Guid characterId)
    {
        var p = Participants.FirstOrDefault(pp => pp.CharacterId == characterId)
            ?? throw new InvalidOperationException("Participant not found");
        if (!p.HasBonusAction) throw new InvalidOperationException("No bonus action available");
        ApplyChange(new CombatBonusActionTaken(Id, characterId));
    }

    /// <summary>
    /// Использовать реакцию участником.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <exception cref="InvalidOperationException">Если участник не найден или реакция недоступна.</exception>
    public void UseReaction(Guid characterId)
    {
        var p = Participants.FirstOrDefault(pp => pp.CharacterId == characterId)
            ?? throw new InvalidOperationException("Participant not found");
        if (!p.HasReaction) throw new InvalidOperationException("No reaction available");
        ApplyChange(new CombatReactionUsed(Id, characterId));
    }

    /// <summary>
    /// Потратить часть перемещения участника.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="feet">Затраченные футы движения.</param>
    /// <exception cref="InvalidOperationException">Если участник не найден или недостаточно движения.</exception>
    public void UseMovement(Guid characterId, int feet)
    {
        var p = Participants.FirstOrDefault(pp => pp.CharacterId == characterId)
            ?? throw new InvalidOperationException("Participant not found");
        if (p.MovementRemaining < feet) throw new InvalidOperationException("Not enough movement");
        ApplyChange(new CombatMovementUsed(Id, characterId, feet));
    }

    /// <summary>
    /// Наложить состояние на участника.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="condition">Название состояния.</param>
    /// <exception cref="InvalidOperationException">Если участник не найден.</exception>
    public void ApplyCondition(Guid characterId, string condition)
    {
        var p = Participants.FirstOrDefault(pp => pp.CharacterId == characterId)
            ?? throw new InvalidOperationException("Participant not found");
        ApplyChange(new ConditionAppliedToCombatant(Id, characterId, condition));
    }

    /// <summary>
    /// Снять состояние с участника.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="condition">Название состояния.</param>
    /// <exception cref="InvalidOperationException">Если участник не найден или состояние отсутствует.</exception>
    public void RemoveCondition(Guid characterId, string condition)
    {
        var p = Participants.FirstOrDefault(pp => pp.CharacterId == characterId)
            ?? throw new InvalidOperationException("Participant not found");
        if (!p.Conditions.Contains(condition)) throw new InvalidOperationException("Condition not present");
        ApplyChange(new ConditionRemovedFromCombatant(Id, characterId, condition));
    }

    /// <summary>
    /// Начать концентрацию для персонажа в бою.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <exception cref="InvalidOperationException">Если участник не найден.</exception>
    public void StartConcentration(Guid characterId)
    {
        var p = Participants.FirstOrDefault(pp => pp.CharacterId == characterId)
            ?? throw new InvalidOperationException("Participant not found");
        ApplyChange(new CombatConcentrationStarted(Id, characterId));
    }

    /// <summary>
    /// Прекратить концентрацию.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <exception cref="InvalidOperationException">Если участник не найден или не концентрируется.</exception>
    public void EndConcentration(Guid characterId)
    {
        var p = Participants.FirstOrDefault(pp => pp.CharacterId == characterId)
            ?? throw new InvalidOperationException("Participant not found");
        if (!p.Concentrating) throw new InvalidOperationException("Not concentrating");
        ApplyChange(new CombatConcentrationEnded(Id, characterId));
    }

    // ---------- Вспомогательный класс ----------

    /// <summary>
    /// Представляет участника боя и его текущее состояние.
    /// </summary>
    public class CombatParticipant(Guid characterId)
    {
        /// <summary>Идентификатор персонажа (участника).</summary>
        public Guid CharacterId { get; } = characterId;

        /// <summary>Инициатива участника.</summary>
        public int Initiative { get; set; }

        /// <summary>Модификатор ловкости, используемый для разрешения ничьих.</summary>
        public int DexterityModifier { get; set; }

        /// <summary>Признак, что сейчас ход этого участника.</summary>
        public bool IsCurrentTurn { get; set; }

        /// <summary>Доступно ли основное действие.</summary>
        public bool HasAction { get; set; }

        /// <summary>Доступно ли бонусное действие.</summary>
        public bool HasBonusAction { get; set; }

        /// <summary>Доступна ли реакция.</summary>
        public bool HasReaction { get; set; }

        /// <summary>Доступно ли движение.</summary>
        public bool HasMovement { get; set; }

        /// <summary>Оставшееся движение в футах.</summary>
        public int MovementRemaining { get; set; }

        /// <summary>Список активных состояний.</summary>
        public List<string> Conditions { get; set; } = [];

        /// <summary>Поддерживает ли концентрацию.</summary>
        public bool Concentrating { get; set; }
    }

    // ---------- Методы, вызываемые CombatHandler ----------

    /// <summary>
    /// Устанавливает инициативу участника (делегирует в <see cref="RollInitiative"/>).
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="initiative">Значение инициативы.</param>
    /// <param name="dexterityModifier">Модификатор ловкости.</param>
    public void SetParticipantInitiative(Guid characterId, int initiative, int dexterityModifier)
        => RollInitiative(characterId, initiative, dexterityModifier);

    /// <summary>
    /// Передаёт ход следующему участнику (по порядку инициативы).
    /// </summary>
    /// <exception cref="InvalidOperationException">Если бой неактивен.</exception>
    public void NextTurn()
    {
        if (!IsActive) throw new InvalidOperationException("Combat not active");
        if (Participants.Count == 0) return;
        int current = CurrentTurnIndex >= 0 ? CurrentTurnIndex : -1;
        int next = (current + 1) % Participants.Count;
        StartTurn(Participants[next].CharacterId);
    }

    /// <summary>
    /// Завершает текущий раунд. В текущей реализации не выполняет действий.
    /// </summary>
    public void EndRound() { /* можно ничего не делать */ }

    /// <summary>
    /// Перемещает участника, тратя его движение.
    /// </summary>
    /// <param name="participantId">Идентификатор участника.</param>
    /// <param name="distanceFeet">Дистанция в футах.</param>
    public void MoveParticipant(Guid participantId, int distanceFeet)
        => UseMovement(participantId, distanceFeet);

    /// <summary>
    /// Выполняет стандартное действие (в текущей реализации просто помечает, что действие использовано).
    /// </summary>
    /// <param name="participantId">Идентификатор участника.</param>
    /// <param name="actionType">Тип действия.</param>
    /// <param name="targetId">Идентификатор цели (если есть).</param>
    /// <param name="actionData">Дополнительные данные действия.</param>
    public void PerformStandardAction(Guid participantId, string actionType, Guid? targetId, object? actionData)
        => UseAction(participantId);

    /// <summary>
    /// Выполняет бонусное действие (помечает, что бонусное действие использовано).
    /// </summary>
    /// <param name="participantId">Идентификатор участника.</param>
    /// <param name="actionType">Тип бонусного действия.</param>
    /// <param name="targetId">Идентификатор цели (если есть).</param>
    /// <param name="actionData">Дополнительные данные.</param>
    public void PerformBonusAction(Guid participantId, string actionType, Guid? targetId, object? actionData)
        => UseBonusAction(participantId);

    /// <summary>
    /// Выполняет реакцию участника.
    /// </summary>
    /// <param name="participantId">Идентификатор участника.</param>
    /// <param name="reactionType">Тип реакции.</param>
    /// <param name="triggerDescription">Описание триггера.</param>
    /// <param name="targetId">Идентификатор цели (если есть).</param>
    public void PerformReaction(Guid participantId, string reactionType, string triggerDescription, Guid? targetId)
        => UseReaction(participantId);

    /// <summary>
    /// Подготавливает действие с условием срабатывания (Ready Action).
    /// </summary>
    /// <param name="participantId">Идентификатор участника.</param>
    /// <param name="actionToReady">Подготавливаемое действие.</param>
    /// <param name="triggerCondition">Условие срабатывания.</param>
    /// <exception cref="InvalidOperationException">Если участник не найден или уже потратил действие/реакцию.</exception>
    public void ReadyAction(Guid participantId, string actionToReady, string triggerCondition)
    {
        var p = Participants.FirstOrDefault(x => x.CharacterId == participantId)
                ?? throw new InvalidOperationException("Participant not found");
        if (!p.HasAction || !p.HasReaction)
            throw new InvalidOperationException("Cannot ready action");
    }

    /// <summary>
    /// Активирует подготовленное действие (расходует реакцию).
    /// </summary>
    /// <param name="participantId">Идентификатор участника.</param>
    public void TriggerReadiedAction(Guid participantId)
        => UseReaction(participantId);

    /// <summary>
    /// Наносит урон цели в бою (создаёт событие, фактическое применение урона — в других агрегатах/проекциях).
    /// </summary>
    /// <param name="sourceParticipantId">Источник урона.</param>
    /// <param name="targetParticipantId">Цель.</param>
    /// <param name="damageAmount">Количество урона.</param>
    /// <param name="damageType">Тип урона.</param>
    public void DealDamage(Guid sourceParticipantId, Guid targetParticipantId, int damageAmount, string damageType)
        => ApplyChange(new CombatDamageDealt(Id, sourceParticipantId, targetParticipantId, damageAmount, damageType));

    /// <summary>
    /// Лечит цель в бою (создаёт событие лечения).
    /// </summary>
    /// <param name="sourceParticipantId">Источник лечения.</param>
    /// <param name="targetParticipantId">Цель.</param>
    /// <param name="healingAmount">Количество восстанавливаемых хитов.</param>
    public void HealTarget(Guid sourceParticipantId, Guid targetParticipantId, int healingAmount)
        => ApplyChange(new CombatHealingDealt(Id, sourceParticipantId, targetParticipantId, healingAmount));

    /// <summary>
    /// Накладывает состояние на участника (делегирует в <see cref="ApplyCondition"/>).
    /// </summary>
    /// <param name="targetParticipantId">Идентификатор цели.</param>
    /// <param name="conditionType">Тип состояния.</param>
    /// <param name="durationRounds">Длительность в раундах (в текущей версии не используется).</param>
    public void ApplyConditionToParticipant(Guid targetParticipantId, string conditionType, int durationRounds)
        => ApplyCondition(targetParticipantId, conditionType);

    /// <summary>
    /// Снимает состояние с участника (делегирует в <see cref="RemoveCondition"/>).
    /// </summary>
    /// <param name="targetParticipantId">Идентификатор цели.</param>
    /// <param name="conditionType">Тип состояния.</param>
    public void RemoveConditionFromParticipant(Guid targetParticipantId, string conditionType)
        => RemoveCondition(targetParticipantId, conditionType);

    /// <summary>
    /// Выполняет спасбросок в бою (создаёт событие; фактическая обработка — во внешних правилах).
    /// </summary>
    /// <param name="participantId">Идентификатор участника.</param>
    /// <param name="ability">Характеристика для спасброска.</param>
    /// <param name="difficultyClass">Сложность.</param>
    /// <param name="rollResult">Результат броска.</param>
    /// <param name="modifiers">Модификаторы.</param>
    public void MakeSavingThrow(Guid participantId, string ability, int difficultyClass, int rollResult, int modifiers)
        => ApplyChange(new CombatSavingThrowMade(Id, participantId, ability, difficultyClass, rollResult, modifiers));

    /// <summary>
    /// Выполняет спасбросок от смерти в бою.
    /// </summary>
    /// <param name="participantId">Идентификатор участника.</param>
    /// <param name="rollResult">Результат броска.</param>
    public void MakeDeathSavingThrow(Guid participantId, int rollResult)
        => ApplyChange(new CombatDeathSavingThrowMade(Id, participantId, rollResult));

    /// <summary>
    /// Стабилизирует участника в бою.
    /// </summary>
    /// <param name="participantId">Идентификатор стабилизируемого.</param>
    /// <param name="stabilizedByParticipantId">Идентификатор того, кто стабилизирует.</param>
    public void StabilizeParticipant(Guid participantId, Guid stabilizedByParticipantId)
        => ApplyChange(new CombatParticipantStabilized(Id, participantId, stabilizedByParticipantId));

    /// <summary>
    /// Выполняет проверку концентрации в бою.
    /// </summary>
    /// <param name="participantId">Идентификатор участника.</param>
    /// <param name="difficultyClass">Сложность.</param>
    /// <param name="rollResult">Результат броска.</param>
    /// <param name="constitutionModifier">Модификатор телосложения.</param>
    public void MakeConcentrationCheck(Guid participantId, int difficultyClass, int rollResult, int constitutionModifier)
        => ApplyChange(new CombatConcentrationCheckMade(Id, participantId, difficultyClass, rollResult, constitutionModifier));

    /// <summary>
    /// Откладывает ход участника.
    /// </summary>
    /// <param name="participantId">Идентификатор участника.</param>
    public void DelayTurn(Guid participantId)
        => ApplyChange(new CombatTurnDelayed(Id, participantId));

    /// <summary>
    /// Помечает участника как сдавшегося.
    /// </summary>
    /// <param name="participantId">Идентификатор участника.</param>
    public void Surrender(Guid participantId)
        => ApplyChange(new CombatSurrender(Id, participantId));
}