using System.Collections.Concurrent;
using dnd_game.Application.Security;
using dnd_game.Application.EventHandlers;
using dnd_game.Application.Services;
using dnd_game.Domain.Events;
using dnd_game.Domain.Sagas;
using dnd_game.Infrastructure.AI;
using dnd_game.Infrastructure.EventStore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace dnd_game.Infrastructure.Common
{
    // ---------- Репозиторий владельцев персонажей ----------
    public class CharacterOwnershipRepository : ICharacterOwnershipRepository
    {
        private readonly ConcurrentDictionary<Guid, Guid> _ownership = new(); // characterId -> userId
        private readonly ConcurrentDictionary<Guid, Guid> _characterCampaigns = new();
        private readonly ConcurrentDictionary<Guid, bool> _npcCharacters = new();

        public Task<bool> IsOwnerAsync(Guid userId, Guid characterId)
        {
            _ownership.TryGetValue(characterId, out var ownerId);
            return Task.FromResult(ownerId == userId);
        }

        public Task<Guid?> GetOwnerAsync(Guid characterId)
        {
            _ownership.TryGetValue(characterId, out var ownerId);
            return Task.FromResult(ownerId == Guid.Empty ? null : (Guid?)ownerId);
        }

        public Task AssignOwnerAsync(Guid characterId, Guid userId)
        {
            _ownership[characterId] = userId;
            return Task.CompletedTask;
        }

        public Guid? GetOwnerId(Guid characterId)
        {
            if (_ownership.TryGetValue(characterId, out var ownerId))
                return ownerId;
            return null;
        }

        public Guid? GetCampaignId(Guid characterId)
        {
            if (_characterCampaigns.TryGetValue(characterId, out var campaignId))
                return campaignId;
            return null;
        }

        public bool IsNonPlayerCharacter(Guid characterId)
        {
            return _npcCharacters.ContainsKey(characterId);
        }

        public List<Guid> GetOwnedCharacterIds(Guid userId)
        {
            return _ownership
                .Where(kvp => kvp.Value == userId)
                .Select(kvp => kvp.Key)
                .ToList();
        }
    }

    // ---------- Репозиторий реплеев ----------
    // Реализует dnd_game.Application.EventHandlers.IReplayEventStore (полный интерфейс с методами),
    // а не пустой одноимённый интерфейс — тот был дублем в глобальном namespace и удалён.
    public class InMemoryReplayEventStore : IReplayEventStore
    {
        private readonly ConcurrentDictionary<Guid, List<IDomainEvent>> _byAggregate = new();
        private readonly ConcurrentDictionary<Guid, List<IDomainEvent>> _bySession = new();
        private readonly object _lock = new();

        public Task AppendAsync(IDomainEvent @event, ReplayMetadata metadata)
        {
            lock (_lock)
            {
                if (@event is IAggregateEvent aggregateEvent)
                {
                    var list = _byAggregate.GetOrAdd(aggregateEvent.AggregateId, _ => new List<IDomainEvent>());
                    list.Add(@event);
                }

                var sessionList = _bySession.GetOrAdd(metadata.SessionId, _ => new List<IDomainEvent>());
                sessionList.Add(@event);
            }

            return Task.CompletedTask;
        }

        public Task<IEnumerable<IDomainEvent>> GetEventsAsync(Guid aggregateId, DateTime? toTimestamp = null)
        {
            if (!_byAggregate.TryGetValue(aggregateId, out var list))
                return Task.FromResult(Enumerable.Empty<IDomainEvent>());

            IEnumerable<IDomainEvent> result = list;
            if (toTimestamp.HasValue)
            {
                result = result.Where(e => e is not ITimestampedEvent timestamped || timestamped.OccurredOn <= toTimestamp.Value);
            }

            return Task.FromResult(result.ToList().AsEnumerable());
        }

        public Task<IEnumerable<IDomainEvent>> GetEventsBySessionAsync(Guid sessionId)
        {
            if (!_bySession.TryGetValue(sessionId, out var list))
                return Task.FromResult(Enumerable.Empty<IDomainEvent>());

            return Task.FromResult(list.ToList().AsEnumerable());
        }

        public Task<long> GetEventCountAsync(Guid aggregateId)
        {
            var count = _byAggregate.TryGetValue(aggregateId, out var list) ? list.Count : 0;
            return Task.FromResult((long)count);
        }

        public Task<IDomainEvent?> GetLastEventAsync(Guid aggregateId)
        {
            if (!_byAggregate.TryGetValue(aggregateId, out var list) || list.Count == 0)
                return Task.FromResult<IDomainEvent?>(null);

            return Task.FromResult<IDomainEvent?>(list[^1]);
        }
    }

    // ---------- Поставщик текущей игровой сессии для ReplayHandler ----------
    // Заглушка по умолчанию: пока в проекте нет концепции "активной" сессии,
    // видимой синглтон-обработчикам, возвращает Guid.Empty (все события пишутся в один "общий" лог).
    public class DefaultCurrentSessionProvider : ICurrentSessionProvider
    {
        public Guid GetCurrentSessionId() => Guid.Empty;
    }

    // ---------- Построитель текстовых записей для реплея/нарратива ----------
    public class DefaultNarrativeLogBuilder : INarrativeLogBuilder
    {
        public string BuildEntry(IDomainEvent @event) => @event.GetType().Name;
    }

    // ---------- Репозиторий триггеров ----------
    public class InMemoryTriggerDefinitionRepository : ITriggerDefinitionRepository
    {
        private readonly ConcurrentDictionary<string, List<TriggerDefinition>> _byEvent = new();

        public IEnumerable<TriggerDefinition> GetByEvent(string eventName)
        {
            return _byEvent.TryGetValue(eventName, out var list) ? list : Enumerable.Empty<TriggerDefinition>();
        }

        public void Add(TriggerDefinition trigger)
        {
            var list = _byEvent.GetOrAdd(trigger.EventName, _ => new List<TriggerDefinition>());
            list.Add(trigger);
        }
    }

    // ---------- Репозиторий вебхуков ----------
    public class InMemoryWebhookSubscriptionRepository : IWebhookSubscriptionRepository
    {
        private readonly ConcurrentDictionary<Guid, WebhookSubscription> _subscriptions = new();

        public Task<IEnumerable<WebhookSubscription>> GetSubscriptionsForEventAsync(string eventType)
        {
            var result = _subscriptions.Values
                .Where(s => s.IsActive && (s.EventType == eventType || s.EventType == "*"))
                .AsEnumerable();
            return Task.FromResult(result);
        }

        public Task AddAsync(WebhookSubscription subscription)
        {
            _subscriptions[subscription.Id] = subscription;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<WebhookSubscription>> GetAllAsync()
        {
            return Task.FromResult(_subscriptions.Values.AsEnumerable());
        }
    }

    // ---------- Репозиторий состояния саг ----------
    public class InMemorySagaStateRepository : ISagaStateRepository
    {
        private readonly ConcurrentDictionary<Guid, ISagaState> _states = new();

        public Task<ISagaState?> LoadAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _states.TryGetValue(id, out var state);
            return Task.FromResult(state);
        }

        public Task SaveAsync(ISagaState state, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);
            _states[state.SagaId] = state;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _states.TryRemove(id, out _);
            return Task.CompletedTask;
        }
    }

    // ---------- Репозиторий рецептов ----------
    public class InMemoryRecipeRepository : IRecipeRepository
    {
        private readonly ConcurrentDictionary<Guid, CraftingRecipe> _recipes = new();

        public CraftingRecipe? GetById(Guid recipeId) => _recipes.TryGetValue(recipeId, out var r) ? r : null;

        public List<CraftingRecipe> GetAll() => _recipes.Values.ToList();

        public List<CraftingRecipe> GetByTool(string toolName) =>
            _recipes.Values.Where(r => r.RequiredTool == toolName).ToList();

        public List<CraftingRecipe> GetBySpell(string spellId) =>
            _recipes.Values.Where(r => r.RequiredSpellId == spellId).ToList();
    }

    // ---------- Репозиторий диалогов ----------
    public class InMemoryDialogueRepository : IDialogueRepository
    {
        // dialogueId -> (nodeId -> node); первый добавленный узел считается корневым.
        private readonly ConcurrentDictionary<Guid, Dictionary<Guid, DialogueNode>> _dialogues = new();
        private readonly ConcurrentDictionary<Guid, Guid> _rootNodeIds = new();

        public DialogueNode? GetRootNode(Guid dialogueId)
        {
            if (!_dialogues.TryGetValue(dialogueId, out var nodes)) return null;
            if (!_rootNodeIds.TryGetValue(dialogueId, out var rootId)) return null;
            return nodes.TryGetValue(rootId, out var node) ? node : null;
        }

        public DialogueNode? GetNode(Guid dialogueId, Guid nodeId)
        {
            if (!_dialogues.TryGetValue(dialogueId, out var nodes)) return null;
            return nodes.TryGetValue(nodeId, out var node) ? node : null;
        }

        public void AddNode(Guid dialogueId, DialogueNode node, bool isRoot = false)
        {
            var nodes = _dialogues.GetOrAdd(dialogueId, _ => new Dictionary<Guid, DialogueNode>());
            nodes[node.NodeId] = node;
            if (isRoot || !_rootNodeIds.ContainsKey(dialogueId))
                _rootNodeIds[dialogueId] = node.NodeId;
        }
    }

    // ---------- Репозиторий AI-скриптов ----------
    public class InMemoryScriptRepository : IScriptRepository
    {
        private readonly ConcurrentDictionary<string, ScriptDefinition> _scripts = new();

        public ScriptDefinition? GetByName(string scriptName) =>
            _scripts.TryGetValue(scriptName, out var s) ? s : null;

        public void AddOrUpdate(ScriptDefinition script) => _scripts[script.ScriptName] = script;

        public List<string> GetAllScriptNames() => _scripts.Keys.ToList();
    }

    // ---------- Репозиторий активных процессов крафта ----------
    public class InMemoryCraftingProcessRepository : ICraftingProcessRepository
    {
        private readonly ConcurrentDictionary<Guid, ActiveCraftingProcess> _processes = new();

        public List<ActiveCraftingProcess> GetActiveForCharacter(Guid characterId) =>
            _processes.Values.Where(p => p.CharacterId == characterId).ToList();

        public ActiveCraftingProcess? GetById(Guid processId) =>
            _processes.TryGetValue(processId, out var p) ? p : null;

        public void Add(ActiveCraftingProcess process) => _processes[process.ProcessId] = process;

        public void Remove(Guid processId) => _processes.TryRemove(processId, out _);

        public void Update(ActiveCraftingProcess process) => _processes[process.ProcessId] = process;
    }

    // ---------- Оценщик условий триггеров ----------
    // Заглушка по умолчанию: считает любое условие выполненным. Настоящую логику
    // (SkillCheck / HasItem / IsAlive / LevelGreaterThan и т.д.) можно добавить позже.
    public class DefaultConditionEvaluator : IConditionEvaluator
    {
        public Task<bool> EvaluateAsync(TriggerCondition condition, IDomainEvent triggeringEvent) =>
            Task.FromResult(true);
    }

    public class InMemoryTradeOfferRepository : ITradeOfferRepository
    {
        private readonly ConcurrentDictionary<Guid, TradeOffer> _offers = new();

        public void Add(TradeOffer offer) => _offers[offer.OfferId] = offer;

        public TradeOffer? GetById(Guid offerId) => _offers.TryGetValue(offerId, out var o) ? o : null;

        public void Update(TradeOffer offer) => _offers[offer.OfferId] = offer;

        public void Remove(Guid offerId) => _offers.TryRemove(offerId, out _);
    }

    public class InMemoryTradeRepository : ITradeRepository
    {
        private readonly ConcurrentDictionary<Guid, Trade> _trades = new();

        public Task<Trade?> GetAsync(Guid id)
        {
            _trades.TryGetValue(id, out var trade);
            return Task.FromResult(trade);
        }

        public Task SaveAsync(Trade trade)
        {
            if (trade != null)
                _trades[trade.Id] = trade;
            return Task.CompletedTask;
        }

        public Application.Services.TradeItem GetItemInfo(string itemId)
        {
            return new Application.Services.TradeItem
            {
                ItemId = itemId,
                ItemName = "Default Item",
                Quantity = 1,
                BasePriceGold = 10,
                IsMagical = false,
                Rarity = "Common"
            };
        }

        public float GetBuyMultiplier(Guid npcId, Guid characterId)
        {
            return 1.0f;
        }

        public float GetSellMultiplier(Guid npcId, Guid characterId)
        {
            return 0.5f;
        }
    }
}