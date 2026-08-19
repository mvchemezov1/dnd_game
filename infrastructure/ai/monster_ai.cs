// infrastructure/ai/monster_ai.cs
using dnd_game.Application.Projections;
using dnd_game.Application.Projections.MaterializedViews;
using dnd_game.Application.Services;        // для CombatService и др., если нужно
using dnd_game.Domain.Aggregates;
using dnd_game.Domain.Rules;
using dnd_game.domain.value_objects;
using dnd_game.Infrastructure.AI;
using dnd_game.infrastructure.message_bus;   // ICommandBus

namespace dnd_game.Infrastructure.AI
{
    /// <summary>
    /// Искусственный интеллект для монстров и неигровых персонажей.
    /// Принимает решения о действиях в бою и вне боя, используя blackboard-память,
    /// тактические правила DnD 5e и информацию о текущем состоянии сцены.
    /// </summary>
    public class MonsterAi
    {
        private readonly IBlackboardStore _blackboard;
        private readonly CharacterProjection _characterProjection;
        private readonly CombatProjection _combatProjection;
        private readonly ICommandBus _commandBus;

        // Минимальные пороги здоровья для смены тактики (в процентах)
        private const float LowHealthThreshold = 0.25f;
        private const float CriticalHealthThreshold = 0.10f;

        public MonsterAi(
            IBlackboardStore blackboard,
            CharacterProjection characterProjection,
            CombatProjection combatProjection,
            ICommandBus commandBus)
        {
            _blackboard = blackboard;
            _characterProjection = characterProjection;
            _combatProjection = combatProjection;
            _commandBus = commandBus;
        }

        /// <summary>
        /// Принять решение о действии для монстра в текущей ситуации.
        /// Возвращает название действия ("attack", "cast_spell", "move", "dash", "disengage",
        /// "dodge", "hide", "help", "ready", "use_item", "flee", "wait").
        /// </summary>
        public async Task<MonsterDecision> DecideAction(Guid monsterId)
        {
            // Очистка устаревших фактов
            await _blackboard.ClearExpiredFacts();

            // Получить состояние монстра
            var monster = await _characterProjection.GetById(monsterId);
            if (monster == null || monster.IsDead || monster.IsUnconscious)
                return MonsterDecision.DoNothing("Monster is incapacitated.");

            // Получить текущий бой, если есть
            CombatStatusDto? combat = null;
            var monsterFacts = await _blackboard.QueryFacts(monsterId, FactType.EntityState);
            var combatFact = monsterFacts.FirstOrDefault(f => f.Key == "CurrentCombatId");
            if (combatFact?.Value is Guid combatId)
            {
                combat = await _combatProjection.GetStatus(combatId);
            }

            // Обновить знания о мире на доске
            await UpdateWorldKnowledge(monsterId, monster, combat);

            // Проверить цели – если есть активная, следовать ей
            var goals = await _blackboard.GetGoals(monsterId);
            if (goals.Any())
            {
                var topGoal = goals.First();
                var goalDecision = PursueGoal(monsterId, topGoal, combat);
                if (goalDecision != null) return goalDecision;
            }

            // Если в бою – боевое поведение
            if (combat != null && combat.IsActive)
            {
                return await DecideCombatAction(monsterId, monster, combat);
            }

            // Вне боя – базовое поведение (патруль, бездействие)
            return DecideOutOfCombatAction(monsterId, monster);
        }

        // --------------------------------------------------------------------------------
        // Обновление знаний
        // --------------------------------------------------------------------------------
        private async Task UpdateWorldKnowledge(Guid monsterId, CharacterDto monster, CombatStatusDto? combat)
        {
            // Запомнить собственное состояние
            await _blackboard.SetFact(monsterId, "HitPoints", monster.HitPoints, FactType.EntityState, expiration: TimeSpan.FromSeconds(30));
            await _blackboard.SetFact(monsterId, "MaxHitPoints", monster.MaxHitPoints, FactType.EntityState, expiration: TimeSpan.FromMinutes(5));
            await _blackboard.SetFact(monsterId, "Conditions", monster.Conditions, FactType.EntityState);

            // Если в бою – обновить информацию о противниках
            if (combat != null)
            {
                await _blackboard.SetFact(monsterId, "CurrentCombatId", combat.CombatId, FactType.EntityState, expiration: TimeSpan.FromSeconds(10));

                foreach (var participant in combat.Participants)
                {
                    if (participant.CharacterId == monsterId) continue;

                    var targetChar = await _characterProjection.GetById(participant.CharacterId);
                    if (targetChar == null) continue;

                    // Противник или союзник? Упрощённо: все остальные – враги (можно уточнить по фракциям)
                    bool isEnemy = true; // заглушка
                    string relation = isEnemy ? "Enemy" : "Ally";

                    await _blackboard.SetFact(monsterId, $"Target_{participant.CharacterId}_HP", targetChar.HitPoints, FactType.EntityState, expiration: TimeSpan.FromSeconds(10));
                    await _blackboard.SetFact(monsterId, $"Target_{participant.CharacterId}_AC", targetChar.ArmorClass, FactType.EntityState, expiration: TimeSpan.FromSeconds(10));
                    await _blackboard.SetFact(monsterId, $"Target_{participant.CharacterId}_Relation", relation, FactType.Relationship, expiration: TimeSpan.FromMinutes(1));
                }

                // Оценить угрозу – самый опасный враг (по ближайшему расстоянию или наибольшему урону)
                await EvaluateThreats(monsterId, monster, combat);
            }
        }

        private async Task EvaluateThreats(Guid monsterId, CharacterDto monster, CombatStatusDto combat)
        {
            var threats = new List<(Guid CharacterId, float ThreatScore)>();
            foreach (var p in combat.Participants)
            {
                if (p.CharacterId == monsterId) continue;
                var relationFact = await _blackboard.GetFact(monsterId, $"Target_{p.CharacterId}_Relation");
                if (relationFact?.Value?.ToString() != "Enemy") continue;

                float distanceScore = 10f; // чем ближе, тем выше (можно рассчитать через позиции)
                float hpScore = (float)(await GetEnemyHitPoints(monsterId, p.CharacterId)) / 100f;
                float threat = 1f / (distanceScore + 1f) * (1f - hpScore) * 10f;
                threats.Add((p.CharacterId, threat));
            }
            var primaryThreat = threats.OrderByDescending(t => t.ThreatScore).FirstOrDefault();
            if (primaryThreat != default)
            {
                await _blackboard.SetFact(monsterId, "PrimaryThreatId", primaryThreat.CharacterId, FactType.Relationship, expiration: TimeSpan.FromSeconds(15));
            }
        }

        // --------------------------------------------------------------------------------
        // Достижение целей
        // --------------------------------------------------------------------------------
        private MonsterDecision? PursueGoal(Guid monsterId, BlackboardGoal goal, CombatStatusDto? combat)
        {
            switch (goal.GoalType)
            {
                case "MoveToLocation":
                    // реализовать движение к точке
                    return MonsterDecision.MoveTo(
                        Convert.ToInt32(goal.Parameters["X"]),
                        Convert.ToInt32(goal.Parameters["Y"]));
                case "AttackTarget":
                    if (goal.Parameters.TryGetValue("TargetId", out var targetIdObj) && targetIdObj is Guid targetId)
                        return MonsterDecision.Attack(targetId);
                    break;
                default:
                    break;
            }
            return null;
        }

        // --------------------------------------------------------------------------------
        // Боевое поведение
        // --------------------------------------------------------------------------------
        private async Task<MonsterDecision> DecideCombatAction(Guid monsterId, CharacterDto monster, CombatStatusDto combat)
        {
            // Проверка, может ли монстр действовать
            if (!CanAct(monster, combat, monsterId))
                return MonsterDecision.DoNothing("Cannot act.");

            var primaryThreatFact = await _blackboard.GetFact(monsterId, "PrimaryThreatId");
            Guid targetId = primaryThreatFact?.Value is Guid g ? g : Guid.Empty;
            if (targetId == Guid.Empty)
            {
                // выбрать ближайшего врага
                foreach (var p in combat.Participants)
                {
                    if (p.CharacterId != monsterId && await IsEnemy(monsterId, p.CharacterId))
                    {
                        targetId = p.CharacterId;
                        break;
                    }
                }
            }
            if (targetId == Guid.Empty)
                return MonsterDecision.DoNothing("No enemies.");

            // 1. Оценить здоровье – бежать, если совсем плохо (инстинкт самосохранения)
            float healthPercent = (float)monster.HitPoints / monster.MaxHitPoints;
            if (healthPercent < CriticalHealthThreshold && CanFlee(monster, combat))
                return MonsterDecision.Flee();

            // 2. Можно ли атаковать основное оружие?
            bool hasAction = combat.Participants.FirstOrDefault(p => p.CharacterId == monsterId)?.HasAction ?? false;
            if (hasAction)
            {
                // Проверить дистанцию до цели (заглушка – 5 футов)
                if (await IsInMeleeRange(monsterId, targetId))
                {
                    return MonsterDecision.Attack(targetId);
                }
                else
                {
                    // Использовать заклинание или дальнобойную атаку, если есть
                    // Вернуть Attack с типом Ranged
                    return MonsterDecision.RangedAttack(targetId);
                }
            }

            // 3. Переместиться к врагу, если не в ближнем бою
            if (await IsInMeleeRange(monsterId, targetId))
            {
                if (!await IsInMeleeRange(monsterId, targetId))
                {
                    return MonsterDecision.MoveTowards(targetId);
                }
            }

            return MonsterDecision.Wait();
        }

        // --------------------------------------------------------------------------------
        // Вне боя
        // --------------------------------------------------------------------------------
        private MonsterDecision DecideOutOfCombatAction(Guid monsterId, CharacterDto monster)
        {
            // Базовое поведение: отдых, патрулирование, поиск еды – зависит от типа монстра
            // Для примера вернём бездействие.
            return MonsterDecision.Wait();
        }

        // --------------------------------------------------------------------------------
        // Вспомогательные проверки (используют правила D&D)
        // --------------------------------------------------------------------------------
        private bool CanAct(CharacterDto monster, CombatStatusDto combat, Guid monsterId)
        {
            var participant = combat.Participants.FirstOrDefault(p => p.CharacterId == monsterId);
            if (participant == null) return false;

            bool isIncapacitated = monster.Conditions.Any(c =>
                c is "Stunned" or "Paralyzed" or "Unconscious" or "Incapacitated" or "Petrified");
            return !isIncapacitated && (participant.HasAction || participant.HasBonusAction || participant.HasMovement);
        }

        private bool CanMove(Guid monsterId, CombatStatusDto combat)
        {
            var participant = combat.Participants.FirstOrDefault(p => p.CharacterId == monsterId);
            return participant != null && participant.HasMovement && participant.MovementRemaining > 0;
        }

        private bool CanFlee(CharacterDto monster, CombatStatusDto combat)
        {
            // Может убежать, если не restrained/grappled и есть движение/действие для Disengage/Dash
            bool isRestrainedOrGrappled = monster.Conditions.Any(c => c is "Restrained" or "Grappled");
            if (isRestrainedOrGrappled) return false;
            var participant = combat.Participants.FirstOrDefault(p => p.CharacterId == monster.Id);
            return participant != null && (participant.HasMovement || participant.HasAction); // Dash
        }

        private async Task<bool> IsEnemy(Guid monsterId, Guid otherId)
        {
            var fact = await _blackboard.GetFact(monsterId, $"Target_{otherId}_Relation");
            return fact?.Value?.ToString() == "Enemy";
        }

        private async Task<bool> IsInMeleeRange(Guid monsterId, Guid targetId)
        {
            // В реальности нужна проверка позиций. Здесь заглушка: считаем, что в начале хода дистанция известна.
            var distanceFact = await _blackboard.GetFact(monsterId, $"Target_{targetId}_Distance");
            if (distanceFact != null && distanceFact.Value is int distanceFeet)
                return distanceFeet <= 5;
            // Предположим, что если нет данных о дальности, то враг рядом (сценарий упрощён)
            return true;
        }

        private async Task<int> GetEnemyHitPoints(Guid monsterId, Guid targetId)
        {
            var fact = await _blackboard.GetFact(monsterId, $"Target_{targetId}_HP");
            return fact?.Value as int? ?? 0;
        }
    }

    /// <summary>
    /// Решение, принятое AI-монстром.
    /// </summary>
    public class MonsterDecision
    {
        public string Action { get; }
        public Guid? TargetId { get; }
        public object? Parameters { get; }
        public string Reason { get; }

        private MonsterDecision(string action, Guid? targetId = null, object? parameters = null, string reason = "")
        {
            Action = action;
            TargetId = targetId;
            Parameters = parameters;
            Reason = reason;
        }

        public static MonsterDecision Attack(Guid targetId) => new("attack", targetId, reason: "Melee attack");
        public static MonsterDecision RangedAttack(Guid targetId) => new("ranged_attack", targetId);
        public static MonsterDecision MoveTowards(Guid targetId) => new("move_towards", targetId);
        public static MonsterDecision MoveTo(int x, int y) => new("move_to", parameters: new Position(x, y));
        public static MonsterDecision Flee() => new("flee", reason: "Fleeing");
        public static MonsterDecision Wait() => new("wait");
        public static MonsterDecision DoNothing(string reason) => new("nothing", reason: reason);
    }
}