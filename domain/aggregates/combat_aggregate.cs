// domain/aggregates/combat_aggregate.cs
using dnd_game.Domain.Events;
using dnd_game.SharedKernel;

namespace dnd_game.Domain.Aggregates;

public class CombatAggregate : AggregateRoot
{
    // ---------- Состояние боя ----------
    public List<CombatParticipant> Participants { get; private set; } = [];
    public bool IsActive { get; private set; }
    public int Round { get; private set; } = 0;
    public int CurrentTurnIndex { get; private set; } = -1; // -1 пока инициатива не определена

    // ---------- Конструкторы ----------
    public CombatAggregate(Guid combatId, IEnumerable<Guid> participantIds)
    {
        ApplyChange(new CombatStarted(combatId, participantIds.ToList(), DateTime.UtcNow));
    }

    public CombatAggregate() { }

    // ---------- Применение событий ----------
    protected override void ApplyEvent(Events.IDomainEvent @event)
    {
        switch (@event)
        {
            case CombatStarted e:
                Id = e.CombatId;
                Participants = e.Participants.Select(id => new CombatParticipant(id)).ToList();
                IsActive = true;
                Round = 0;
                CurrentTurnIndex = -1;
                break;
            case CombatEnded e:
                IsActive = false;
                break;
            case InitiativeRolled e:
                var participant = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                if (participant != null)
                {
                    participant.Initiative = e.Initiative;
                    participant.DexterityModifier = e.DexterityModifier; // для разрешения ничьих
                }
                // Пересортировка и установка порядка происходит явно при старте раунда
                break;
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
            case CombatTurnStarted e:
                // Найти участника и установить IsCurrentTurn
                var turnParticipant = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                if (turnParticipant != null)
                {
                    // Сброс действий для нового хода
                    turnParticipant.IsCurrentTurn = true;
                    turnParticipant.HasAction = true;
                    turnParticipant.HasBonusAction = true;
                    turnParticipant.HasReaction = true; // реакция восстанавливается в начале СВОЕГО хода
                    turnParticipant.HasMovement = true;
                    turnParticipant.MovementRemaining = 30;
                }
                CurrentTurnIndex = Participants.FindIndex(p => p.CharacterId == e.CharacterId);
                break;
            case CombatTurnEnded e:
                var endedParticipant = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                if (endedParticipant != null)
                {
                    endedParticipant.IsCurrentTurn = false;
                }
                break;
            case ParticipantAddedToCombat e:
                if (!Participants.Any(p => p.CharacterId == e.CharacterId))
                {
                    Participants.Add(new CombatParticipant(e.CharacterId)
                    {
                        Initiative = e.Initiative
                    });
                }
                break;
            case ParticipantRemovedFromCombat e:
                Participants.RemoveAll(p => p.CharacterId == e.CharacterId);
                // Если удалили текущего, нужно переопределить порядок (но лучше оставить как есть, следующий ход будет корректным)
                break;
            case CombatActionTaken e:
                var actor = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                if (actor != null) actor.HasAction = false;
                break;
            case CombatBonusActionTaken e:
                var bonusActor = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                if (bonusActor != null) bonusActor.HasBonusAction = false;
                break;
            case CombatReactionUsed e:
                var reactor = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                if (reactor != null) reactor.HasReaction = false;
                break;
            case CombatMovementUsed e:
                var mover = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                if (mover != null) mover.MovementRemaining = Math.Max(0, mover.MovementRemaining - e.Feet);
                break;
            case ConditionAppliedToCombatant e:
                var target = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                if (target != null && !target.Conditions.Contains(e.Condition))
                    target.Conditions.Add(e.Condition);
                break;
            case ConditionRemovedFromCombatant e:
                var condTarget = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                condTarget?.Conditions.Remove(e.Condition);
                break;
            case CombatConcentrationStarted e:
                var conc = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                if (conc != null) conc.Concentrating = true;
                break;
            case CombatConcentrationEnded e:
                var concEnd = Participants.FirstOrDefault(p => p.CharacterId == e.CharacterId);
                if (concEnd != null) concEnd.Concentrating = false;
                break;
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

    public void EndCombat()
    {
        if (!IsActive) throw new InvalidOperationException("Combat not active");
        ApplyChange(new CombatEnded(Id, DateTime.UtcNow));
    }

    /// <summary>
    /// Игрок/Мастер бросает инициативу для персонажа.
    /// </summary>
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
    public void StartRound()
    {
        if (!IsActive) throw new InvalidOperationException("Combat not active");
        // Проверяем, что у всех есть инициатива (иначе нельзя начинать)
        if (Participants.Any(p => p.Initiative == 0 && p.CharacterId != Guid.Empty))
            throw new InvalidOperationException("Not all participants have rolled initiative.");
        ApplyChange(new CombatRoundStarted(Id, Round + 1, DateTime.UtcNow));
    }

    /// <summary>
    /// Начать ход указанного персонажа. Обычно вызывается автоматически после окончания предыдущего хода.
    /// </summary>
    public void StartTurn(Guid characterId)
    {
        if (!IsActive) throw new InvalidOperationException("Combat not active");
        var participant = Participants.FirstOrDefault(p => p.CharacterId == characterId)
            ?? throw new InvalidOperationException("Participant not found");
        ApplyChange(new CombatTurnStarted(Id, characterId, DateTime.UtcNow));
    }

    /// <summary>
    /// Завершить текущий ход.
    /// </summary>
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
    public void AddParticipant(Guid characterId, int initiative)
    {
        if (!IsActive) throw new InvalidOperationException("Combat not active");
        ApplyChange(new ParticipantAddedToCombat(Id, characterId, initiative));
    }

    /// <summary>
    /// Удалить участника из боя (смерть/бегство).
    /// </summary>
    public void RemoveParticipant(Guid characterId)
    {
        if (!IsActive) throw new InvalidOperationException("Combat not active");
        if (!Participants.Any(p => p.CharacterId == characterId))
            throw new InvalidOperationException("Participant not found");
        ApplyChange(new ParticipantRemovedFromCombat(Id, characterId));
    }

    /// <summary>
    /// Использовать основное действие.
    /// </summary>
    public void UseAction(Guid characterId)
    {
        var p = Participants.FirstOrDefault(pp => pp.CharacterId == characterId)
            ?? throw new InvalidOperationException("Participant not found");
        if (!p.HasAction) throw new InvalidOperationException("No action available");
        ApplyChange(new CombatActionTaken(Id, characterId));
    }

    /// <summary>
    /// Использовать бонусное действие.
    /// </summary>
    public void UseBonusAction(Guid characterId)
    {
        var p = Participants.FirstOrDefault(pp => pp.CharacterId == characterId)
            ?? throw new InvalidOperationException("Participant not found");
        if (!p.HasBonusAction) throw new InvalidOperationException("No bonus action available");
        ApplyChange(new CombatBonusActionTaken(Id, characterId));
    }

    /// <summary>
    /// Использовать реакцию.
    /// </summary>
    public void UseReaction(Guid characterId)
    {
        var p = Participants.FirstOrDefault(pp => pp.CharacterId == characterId)
            ?? throw new InvalidOperationException("Participant not found");
        if (!p.HasReaction) throw new InvalidOperationException("No reaction available");
        ApplyChange(new CombatReactionUsed(Id, characterId));
    }

    /// <summary>
    /// Потратить часть перемещения.
    /// </summary>
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
    public void ApplyCondition(Guid characterId, string condition)
    {
        var p = Participants.FirstOrDefault(pp => pp.CharacterId == characterId)
            ?? throw new InvalidOperationException("Participant not found");
        ApplyChange(new ConditionAppliedToCombatant(Id, characterId, condition));
    }

    /// <summary>
    /// Снять состояние с участника.
    /// </summary>
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
    public void StartConcentration(Guid characterId)
    {
        var p = Participants.FirstOrDefault(pp => pp.CharacterId == characterId)
            ?? throw new InvalidOperationException("Participant not found");
        ApplyChange(new CombatConcentrationStarted(Id, characterId));
    }

    /// <summary>
    /// Прекратить концентрацию.
    /// </summary>
    public void EndConcentration(Guid characterId)
    {
        var p = Participants.FirstOrDefault(pp => pp.CharacterId == characterId)
            ?? throw new InvalidOperationException("Participant not found");
        if (!p.Concentrating) throw new InvalidOperationException("Not concentrating");
        ApplyChange(new CombatConcentrationEnded(Id, characterId));
    }

    // ---------- Вспомогательный класс ----------
    public class CombatParticipant(Guid characterId)
    {
        public Guid CharacterId { get; } = characterId;
        public int Initiative { get; set; }
        public int DexterityModifier { get; set; }
        public bool IsCurrentTurn { get; set; }
        public bool HasAction { get; set; }
        public bool HasBonusAction { get; set; }
        public bool HasReaction { get; set; }
        public bool HasMovement { get; set; }
        public int MovementRemaining { get; set; }
        public List<string> Conditions { get; set; } = [];
        public bool Concentrating { get; set; }
    }

    // ---------- Методы, вызываемые CombatHandler ----------
    public void SetParticipantInitiative(Guid characterId, int initiative, int dexterityModifier)
        => RollInitiative(characterId, initiative, dexterityModifier);

    public void NextTurn()
    {
        if (!IsActive) throw new InvalidOperationException("Combat not active");
        if (Participants.Count == 0) return;
        int current = CurrentTurnIndex >= 0 ? CurrentTurnIndex : -1;
        int next = (current + 1) % Participants.Count;
        StartTurn(Participants[next].CharacterId);
    }

    public void EndRound() { /* можно ничего не делать */ }

    public void MoveParticipant(Guid participantId, int distanceFeet)
        => UseMovement(participantId, distanceFeet);

    public void PerformStandardAction(Guid participantId, string actionType, Guid? targetId, object? actionData)
        => UseAction(participantId);

    public void PerformBonusAction(Guid participantId, string actionType, Guid? targetId, object? actionData)
        => UseBonusAction(participantId);

    public void PerformReaction(Guid participantId, string reactionType, string triggerDescription, Guid? targetId)
        => UseReaction(participantId);

    public void ReadyAction(Guid participantId, string actionToReady, string triggerCondition)
    {
        var p = Participants.FirstOrDefault(x => x.CharacterId == participantId)
                ?? throw new InvalidOperationException("Participant not found");
        if (!p.HasAction || !p.HasReaction)
            throw new InvalidOperationException("Cannot ready action");
    }

    public void TriggerReadiedAction(Guid participantId)
        => UseReaction(participantId);

    public void DealDamage(Guid sourceParticipantId, Guid targetParticipantId, int damageAmount, string damageType)
        => ApplyChange(new CombatDamageDealt(Id, sourceParticipantId, targetParticipantId, damageAmount, damageType));

    public void HealTarget(Guid sourceParticipantId, Guid targetParticipantId, int healingAmount)
        => ApplyChange(new CombatHealingDealt(Id, sourceParticipantId, targetParticipantId, healingAmount));

    public void ApplyConditionToParticipant(Guid targetParticipantId, string conditionType, int durationRounds)
        => ApplyCondition(targetParticipantId, conditionType);

    public void RemoveConditionFromParticipant(Guid targetParticipantId, string conditionType)
        => RemoveCondition(targetParticipantId, conditionType);

    public void MakeSavingThrow(Guid participantId, string ability, int difficultyClass, int rollResult, int modifiers)
        => ApplyChange(new CombatSavingThrowMade(Id, participantId, ability, difficultyClass, rollResult, modifiers));

    public void MakeDeathSavingThrow(Guid participantId, int rollResult)
        => ApplyChange(new CombatDeathSavingThrowMade(Id, participantId, rollResult));

    public void StabilizeParticipant(Guid participantId, Guid stabilizedByParticipantId)
        => ApplyChange(new CombatParticipantStabilized(Id, participantId, stabilizedByParticipantId));

    public void MakeConcentrationCheck(Guid participantId, int difficultyClass, int rollResult, int constitutionModifier)
        => ApplyChange(new CombatConcentrationCheckMade(Id, participantId, difficultyClass, rollResult, constitutionModifier));

    public void DelayTurn(Guid participantId)
        => ApplyChange(new CombatTurnDelayed(Id, participantId));

    public void Surrender(Guid participantId)
        => ApplyChange(new CombatSurrender(Id, participantId));
}