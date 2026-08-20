// infrastructure/ai/npc_behavior_tree.cs
using dnd_game.Domain.Commands;
using dnd_game.Application.Projections;
using dnd_game.Infrastructure.AI;
using System.Collections.Concurrent;
using dnd_game.infrastructure.message_bus;

namespace dnd_game.Infrastructure.AI
{
    // ===================================================================================
    // Статус выполнения узла дерева поведения
    // ===================================================================================
    public enum BehaviorStatus
    {
        Success,
        Failure,
        Running
    }

    // ===================================================================================
    // Контекст выполнения дерева поведения
    // ===================================================================================
    public class BehaviorTreeContext
    {
        public Guid NpcId { get; }
        public IBlackboardStore Blackboard { get; }
        public CharacterProjection CharacterProjection { get; }
        public CombatProjection CombatProjection { get; }
        public CampaignProjection CampaignProjection { get; }
        public ICommandBus CommandBus { get; }

        // Локальный кэш данных NPC, обновляется перед каждым выполнением дерева
        public CharacterDto? SelfCharacter { get; set; }
        public CombatStatusDto? ActiveCombat { get; set; }

        public BehaviorTreeContext(
            Guid npcId,
            IBlackboardStore blackboard,
            CharacterProjection characterProjection,
            CombatProjection combatProjection,
            CampaignProjection campaignProjection,
            ICommandBus commandBus)
        {
            NpcId = npcId;
            Blackboard = blackboard;
            CharacterProjection = characterProjection;
            CombatProjection = combatProjection;
            CampaignProjection = campaignProjection;
            CommandBus = commandBus;
        }
    }

    // ===================================================================================
    // Абстрактный узел дерева поведения
    // ===================================================================================
    public abstract class BehaviorTreeNode
    {
        public abstract Task<BehaviorStatus> Execute(BehaviorTreeContext context);
    }

    // ===================================================================================
    // Композитные узлы
    // ===================================================================================

    /// <summary>
    /// Последовательность (Sequence) – выполняет дочерние узлы по порядку,
    /// пока все не вернут Success. Если один возвращает Failure, последовательность
    /// немедленно завершается с Failure. Если Running – возвращает Running.
    /// </summary>
    public class SequenceNode : BehaviorTreeNode
    {
        private readonly List<BehaviorTreeNode> _children;
        public SequenceNode(List<BehaviorTreeNode> children) => _children = children;

        public override async Task<BehaviorStatus> Execute(BehaviorTreeContext context)
        {
            foreach (var child in _children)
            {
                var status = await child.Execute(context);
                if (status != BehaviorStatus.Success)
                    return status;
            }
            return BehaviorStatus.Success;
        }
    }

    /// <summary>
    /// Селектор (Selector) – выполняет дочерние узлы по порядку,
    /// пока один не вернет Success. Если все вернут Failure – возвращает Failure.
    /// При Running возвращает Running и запоминает активного ребёнка для следующего тика.
    /// </summary>
    public class SelectorNode : BehaviorTreeNode
    {
        private readonly List<BehaviorTreeNode> _children;
        private int _runningIndex = -1;

        public SelectorNode(List<BehaviorTreeNode> children) => _children = children;

        public override async Task<BehaviorStatus> Execute(BehaviorTreeContext context)
        {
            int start = _runningIndex >= 0 ? _runningIndex : 0;
            for (int i = start; i < _children.Count; i++)
            {
                var status = await _children[i].Execute(context);
                if (status == BehaviorStatus.Success)
                {
                    _runningIndex = -1;
                    return BehaviorStatus.Success;
                }
                if (status == BehaviorStatus.Running)
                {
                    _runningIndex = i;
                    return BehaviorStatus.Running;
                }
            }
            _runningIndex = -1;
            return BehaviorStatus.Failure;
        }
    }

    /// <summary>
    /// Параллельный узел (Parallel) – запускает всех детей одновременно.
    /// Возвращает Success, если заданное количество детей завершилось успехом.
    /// </summary>
    public class ParallelNode : BehaviorTreeNode
    {
        private readonly List<BehaviorTreeNode> _children;
        private readonly int _requiredSuccesses;

        public ParallelNode(List<BehaviorTreeNode> children, int requiredSuccesses)
        {
            _children = children;
            _requiredSuccesses = requiredSuccesses;
        }

        public override async Task<BehaviorStatus> Execute(BehaviorTreeContext context)
        {
            var tasks = _children.Select(c => c.Execute(context)).ToArray();
            await Task.WhenAll(tasks);
            int successes = tasks.Count(t => t.Result == BehaviorStatus.Success);
            return successes >= _requiredSuccesses ? BehaviorStatus.Success : BehaviorStatus.Failure;
        }
    }

    // ===================================================================================
    // Декораторы
    // ===================================================================================
    public class InverterNode : BehaviorTreeNode
    {
        private readonly BehaviorTreeNode _child;
        public InverterNode(BehaviorTreeNode child) => _child = child;

        public override async Task<BehaviorStatus> Execute(BehaviorTreeContext context)
        {
            var status = await _child.Execute(context);
            return status switch
            {
                BehaviorStatus.Success => BehaviorStatus.Failure,
                BehaviorStatus.Failure => BehaviorStatus.Success,
                _ => status
            };
        }
    }

    public class RepeaterNode : BehaviorTreeNode
    {
        private readonly BehaviorTreeNode _child;
        private readonly int _maxRepeats;
        private int _count;

        public RepeaterNode(BehaviorTreeNode child, int maxRepeats = -1)
        {
            _child = child;
            _maxRepeats = maxRepeats;
        }

        public override async Task<BehaviorStatus> Execute(BehaviorTreeContext context)
        {
            while (_maxRepeats == -1 || _count < _maxRepeats)
            {
                var status = await _child.Execute(context);
                if (status == BehaviorStatus.Failure) return BehaviorStatus.Failure;
                if (status == BehaviorStatus.Running) return BehaviorStatus.Running;
                _count++;
                // успех – продолжаем повторять
            }
            return BehaviorStatus.Success;
        }
    }

    public class UntilSuccessNode : BehaviorTreeNode
    {
        private readonly BehaviorTreeNode _child;
        public UntilSuccessNode(BehaviorTreeNode child) => _child = child;

        public override async Task<BehaviorStatus> Execute(BehaviorTreeContext context)
        {
            var status = await _child.Execute(context);
            if (status == BehaviorStatus.Failure) return BehaviorStatus.Running; // продолжим на следующем тике
            return status;
        }
    }

    // ===================================================================================
    // Условия (листья, возвращают Success/Failure)
    // ===================================================================================

    /// <summary>
    /// Условие, проверяемое через асинхронную функцию.
    /// </summary>
    public class ConditionNode : BehaviorTreeNode
    {
        private readonly Func<BehaviorTreeContext, Task<bool>> _condition;

        public ConditionNode(Func<BehaviorTreeContext, Task<bool>> condition) => _condition = condition;

        public override async Task<BehaviorStatus> Execute(BehaviorTreeContext context)
        {
            return await _condition(context) ? BehaviorStatus.Success : BehaviorStatus.Failure;
        }
    }

    // Статические фабрики для типичных условий D&D

    public static class BehaviorTreeConditions
    {
        public static ConditionNode IsAlive() =>
            new ConditionNode(async ctx =>
            {
                var character = ctx.SelfCharacter ?? await ctx.CharacterProjection.GetById(ctx.NpcId);
                return character != null && !character.IsDead && character.HitPoints > 0;
            });

        public static ConditionNode HealthAbovePercent(float percent) =>
            new ConditionNode(async ctx =>
            {
                var character = ctx.SelfCharacter ?? await ctx.CharacterProjection.GetById(ctx.NpcId);
                if (character == null || character.MaxHitPoints == 0) return false;
                return (float)character.HitPoints / character.MaxHitPoints >= percent;
            });

        public static ConditionNode HasEnemyInCombat() =>
            new ConditionNode(async ctx =>
            {
                if (ctx.ActiveCombat == null) return false;
                foreach (var participant in ctx.ActiveCombat.Participants)
                {
                    if (participant.CharacterId == ctx.NpcId) continue;
                    // проверяем, является ли врагом (можно через Blackboard факт Target_Relation)
                    var fact = await ctx.Blackboard.GetFact(ctx.NpcId, $"Target_{participant.CharacterId}_Relation");
                    if (fact?.Value?.ToString() == "Enemy") return true;
                }
                return false;
            });

        public static ConditionNode IsInCombat() =>
            new ConditionNode(ctx => Task.FromResult(ctx.ActiveCombat != null && ctx.ActiveCombat.IsActive));

        public static ConditionNode IsMyTurn() =>
            new ConditionNode(ctx =>
            {
                if (ctx.ActiveCombat == null) return Task.FromResult(false);
                var current = ctx.ActiveCombat.Participants.FirstOrDefault(p => p.IsCurrentTurn);
                return Task.FromResult(current?.CharacterId == ctx.NpcId);
            });

        public static ConditionNode IsWithinMeleeRange(Guid targetId) =>
            new ConditionNode(async ctx =>
            {
                var distanceFact = await ctx.Blackboard.GetFact(ctx.NpcId, $"Target_{targetId}_Distance");
                return distanceFact?.Value is int distance && distance <= 5;
            });

        public static ConditionNode HasItem(string itemId) =>
            new ConditionNode(async ctx =>
            {
                var character = ctx.SelfCharacter ?? await ctx.CharacterProjection.GetById(ctx.NpcId);
                return character?.Inventory?.Any(i => i.ItemId == itemId) ?? false;
            });

        public static ConditionNode IsDaytime() =>
            new ConditionNode(async ctx =>
            {
                var campaignFact = await ctx.Blackboard.GetFact(ctx.NpcId, "CampaignId");
                if (campaignFact?.Value is not Guid campaignId) return true;
                var state = await ctx.CampaignProjection.GetCampaignState(campaignId);
                return state == null || (state.Hour >= 6 && state.Hour < 18);
            });
    }

    // ===================================================================================
    // Действия (листья, выполняют команду и возвращают Success/Running/Failure)
    // ===================================================================================
    public class ActionNode : BehaviorTreeNode
    {
        private readonly Func<BehaviorTreeContext, Task<BehaviorStatus>> _action;

        public ActionNode(Func<BehaviorTreeContext, Task<BehaviorStatus>> action) => _action = action;

        public override async Task<BehaviorStatus> Execute(BehaviorTreeContext context)
        {
            return await _action(context);
        }
    }

    // Статические фабрики для действий D&D
    public static class BehaviorTreeActions
    {
        public static ActionNode Attack(Guid targetId) =>
            new ActionNode(async ctx =>
            {
                // Отправляем команду атаки (в рамках боя или вне)
                await ctx.CommandBus.SendAsync(new TakeStandardAction(
                    ctx.ActiveCombat?.CombatId ?? Guid.Empty,
                    ctx.NpcId, "Attack", targetId));
                return BehaviorStatus.Success;
            });

        public static ActionNode MoveToTarget(Guid targetId) =>
            new ActionNode(async ctx =>
            {
                await ctx.CommandBus.SendAsync(new MoveCharacter(ctx.NpcId, 0, 0)); // заглушка; должно быть перемещение к цели
                return BehaviorStatus.Success;
            });

        public static ActionNode Flee() =>
            new ActionNode(async ctx =>
            {
                await ctx.CommandBus.SendAsync(new TakeStandardAction(
                    ctx.ActiveCombat?.CombatId ?? Guid.Empty,
                    ctx.NpcId, "Dash", null));
                // и перемещение прочь от врага – реализуется через Move команду
                return BehaviorStatus.Success;
            });

        public static ActionNode Wait() =>
            new ActionNode(ctx => Task.FromResult(BehaviorStatus.Success));

        public static ActionNode UseHealingPotion() =>
            new ActionNode(async ctx =>
            {
                var character = ctx.SelfCharacter ?? await ctx.CharacterProjection.GetById(ctx.NpcId);
                var potion = character?.Inventory?.FirstOrDefault(i => i.Name.Contains("Potion of Healing"));
                if (potion != null)
                {
                    await ctx.CommandBus.SendAsync(new HealCharacter(ctx.NpcId, 7)); // 2d4+2 среднее
                    await ctx.CommandBus.SendAsync(new RemoveInventoryItem(ctx.NpcId, potion.ItemId, 1));
                    return BehaviorStatus.Success;
                }
                return BehaviorStatus.Failure;
            });

        public static ActionNode Patrol(string routeId) =>
            new ActionNode(async ctx =>
            {
                // Отправить команду перемещения по точкам маршрута
                await Task.Delay(100); // имитация
                return BehaviorStatus.Running; // патрулирование продолжается
            });
    }

    // ===================================================================================
    // Класс дерева поведения
    // ===================================================================================
    public class NpcBehaviorTree
    {
        private readonly BehaviorTreeNode _root;
        private readonly BehaviorTreeContext _context;
        private DateTime _lastTick;

        public NpcBehaviorTree(BehaviorTreeNode root, BehaviorTreeContext context)
        {
            _root = root;
            _context = context;
        }

        /// <summary>
        /// Основной тик дерева. Должен вызываться с частотой, достаточной для реакций на события
        /// (например, каждый раунд боя или каждые 5 секунд реального времени).
        /// </summary>
        public async Task Tick()
        {
            // Обновляем контекстные данные перед тиком
            await RefreshContext();

            await _root.Execute(_context);
            _lastTick = DateTime.UtcNow;
        }

        private async Task RefreshContext()
        {
            _context.SelfCharacter = await _context.CharacterProjection.GetById(_context.NpcId);
            // Определяем активный бой
            var combatFact = await _context.Blackboard.GetFact(_context.NpcId, "CurrentCombatId");
            if (combatFact?.Value is Guid combatId)
            {
                _context.ActiveCombat = await _context.CombatProjection.GetStatus(combatId);
            }
            else
            {
                _context.ActiveCombat = null;
            }
            // Очистка устаревших фактов (делаем раз в несколько тиков)
            if ((DateTime.UtcNow - _lastTick).TotalSeconds > 10)
                await _context.Blackboard.ClearExpiredFacts();
        }
    }
}