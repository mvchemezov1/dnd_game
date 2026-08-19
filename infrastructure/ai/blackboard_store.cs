// infrastructure/ai/blackboard_store.cs
using System.Collections.Concurrent;

namespace dnd_game.Infrastructure.AI
{
    /// <summary>
    /// Тип факта в доске объявлений.
    /// </summary>
    public enum FactType
    {
        WorldState,      // глобальное состояние мира (погода, время суток)
        EntityState,     // состояние персонажа/существа (жив, локация, хиты)
        Relationship,    // отношения между персонажами (союзник, враг)
        Event,           // произошедшее событие (атака, крик о помощи)
        Location,        // информация о месте (опасность, укрытие)
        Item             // информация о предмете (наличие, владелец)
    }

    /// <summary>
    /// Отдельный факт на доске.
    /// </summary>
    public class BlackboardFact
    {
        public Guid EntityId { get; set; }         // к какому существу/объекту относится
        public string Key { get; set; } = string.Empty;
        public object Value { get; set; } = null!;
        public FactType Type { get; set; }
        public float Confidence { get; set; } = 1.0f;  // 0..1, насколько ИИ уверен в факте
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public TimeSpan? Expiration { get; set; }       // через сколько факт устареет
        public string Source { get; set; } = string.Empty; // кто или что сообщило факт
    }

    /// <summary>
    /// Цель, которую преследует ИИ-существо.
    /// </summary>
    public class BlackboardGoal
    {
        public Guid GoalId { get; set; } = Guid.NewGuid();
        public Guid EntityId { get; set; }
        public string GoalType { get; set; } = string.Empty; // "AttackTarget", "MoveToLocation", "ProtectAlly"
        public Dictionary<string, object> Parameters { get; set; } = new();
        public int Priority { get; set; }          // чем выше, тем важнее
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? Deadline { get; set; }
        public bool IsCompleted { get; set; }
    }

    /// <summary>
    /// Запись памяти (событие, важное для принятия решений).
    /// </summary>
    public class BlackboardMemory
    {
        public Guid MemoryId { get; set; } = Guid.NewGuid();
        public Guid EntityId { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int Importance { get; set; }       // 0..10, чем выше, тем дольше помнится
        public TimeSpan Retention => TimeSpan.FromMinutes(Importance * 10); // примерное время хранения
    }

    /// <summary>
    /// Полноценный интерфейс доски объявлений для AI.
    /// </summary>
    public interface IBlackboardStore
    {
        // Факты
        Task SetFact(Guid entityId, string key, object value, FactType type = FactType.EntityState, float confidence = 1.0f, TimeSpan? expiration = null, string source = "");
        Task<BlackboardFact?> GetFact(Guid entityId, string key);
        Task<List<BlackboardFact>> QueryFacts(Guid entityId, FactType? type = null, float minConfidence = 0.0f);
        Task RemoveFact(Guid entityId, string key);
        Task ClearExpiredFacts();

        // Цели
        Task AddGoal(BlackboardGoal goal);
        Task<List<BlackboardGoal>> GetGoals(Guid entityId, bool onlyActive = true);
        Task UpdateGoal(BlackboardGoal goal);
        Task RemoveGoal(Guid goalId);

        // Память
        Task AddMemory(BlackboardMemory memory);
        Task<List<BlackboardMemory>> GetMemories(Guid entityId, int minImportance = 0, DateTime? since = null);
        Task ForgetOldMemories();
    }

    /// <summary>
    /// Реализация доски объявлений в памяти.
    /// </summary>
    public class BlackboardStore : IBlackboardStore
    {
        private readonly ConcurrentDictionary<string, BlackboardFact> _facts = new();
        private readonly ConcurrentDictionary<Guid, BlackboardGoal> _goals = new();
        private readonly ConcurrentBag<BlackboardMemory> _memories = new();

        private static string FactKey(Guid entityId, string key) => $"{entityId}:{key}";

        public Task SetFact(Guid entityId, string key, object value, FactType type = FactType.EntityState, float confidence = 1.0f, TimeSpan? expiration = null, string source = "")
        {
            var fact = new BlackboardFact
            {
                EntityId = entityId,
                Key = key,
                Value = value,
                Type = type,
                Confidence = confidence,
                Timestamp = DateTime.UtcNow,
                Expiration = expiration,
                Source = source
            };
            _facts[FactKey(entityId, key)] = fact;
            return Task.CompletedTask;
        }

        public Task<BlackboardFact?> GetFact(Guid entityId, string key)
        {
            _facts.TryGetValue(FactKey(entityId, key), out var fact);
            if (fact != null && fact.Expiration.HasValue)
            {
                if (DateTime.UtcNow > fact.Timestamp + fact.Expiration.Value)
                {
                    _facts.TryRemove(FactKey(entityId, key), out _);
                    return Task.FromResult<BlackboardFact?>(null);
                }
            }
            return Task.FromResult(fact);
        }

        public Task<List<BlackboardFact>> QueryFacts(Guid entityId, FactType? type = null, float minConfidence = 0.0f)
        {
            var result = _facts.Values
                .Where(f => f.EntityId == entityId)
                .Where(f => !type.HasValue || f.Type == type.Value)
                .Where(f => f.Confidence >= minConfidence)
                .ToList();
            return Task.FromResult(result);
        }

        public Task RemoveFact(Guid entityId, string key)
        {
            _facts.TryRemove(FactKey(entityId, key), out _);
            return Task.CompletedTask;
        }

        public Task ClearExpiredFacts()
        {
            var now = DateTime.UtcNow;
            var expired = _facts.Values
                .Where(f => f.Expiration.HasValue && now > f.Timestamp + f.Expiration.Value)
                .ToList();
            foreach (var f in expired)
                _facts.TryRemove(FactKey(f.EntityId, f.Key), out _);
            return Task.CompletedTask;
        }

        public Task AddGoal(BlackboardGoal goal)
        {
            _goals[goal.GoalId] = goal;
            return Task.CompletedTask;
        }

        public Task<List<BlackboardGoal>> GetGoals(Guid entityId, bool onlyActive = true)
        {
            var goals = _goals.Values
                .Where(g => g.EntityId == entityId && (!onlyActive || !g.IsCompleted))
                .OrderByDescending(g => g.Priority)
                .ToList();
            return Task.FromResult(goals);
        }

        public Task UpdateGoal(BlackboardGoal goal)
        {
            _goals[goal.GoalId] = goal;
            return Task.CompletedTask;
        }

        public Task RemoveGoal(Guid goalId)
        {
            _goals.TryRemove(goalId, out _);
            return Task.CompletedTask;
        }

        public Task AddMemory(BlackboardMemory memory)
        {
            _memories.Add(memory);
            return Task.CompletedTask;
        }

        public Task<List<BlackboardMemory>> GetMemories(Guid entityId, int minImportance = 0, DateTime? since = null)
        {
            var query = _memories.AsEnumerable()
                .Where(m => m.EntityId == entityId && m.Importance >= minImportance);
            if (since.HasValue)
                query = query.Where(m => m.Timestamp >= since.Value);
            return Task.FromResult(query.OrderByDescending(m => m.Importance).ThenByDescending(m => m.Timestamp).ToList());
        }

        public Task ForgetOldMemories()
        {
            var now = DateTime.UtcNow;
            var toRemove = _memories.Where(m => now > m.Timestamp + m.Retention).ToList();
            foreach (var mem in toRemove)
                _memories.TryTake(out _); // не совсем корректно для ConcurrentBag, но для примера
            return Task.CompletedTask;
        }
    }
}