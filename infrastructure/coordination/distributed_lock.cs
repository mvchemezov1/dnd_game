// infrastructure/coordination/distributed_lock.cs
using Microsoft.Extensions.Logging;
using StackExchange.Redis; // пример для Redis; можно заменить на консул или другой бэкенд

namespace dnd_game.Infrastructure.Coordination
{
    /// <summary>
    /// Тип ресурса для блокировки в мире DnD.
    /// </summary>
    public enum LockResourceType
    {
        Character,      // Character:{id}
        Combat,         // Combat:{id}
        Campaign,       // Campaign:{id}
        Inventory,      // Inventory:{characterId}
        Trade,          // Trade:{offerId}
        Global          // глобальная блокировка (например, смена времени суток)
    }

    /// <summary>
    /// Режим блокировки.
    /// </summary>
    public enum LockMode
    {
        Exclusive,      // полная блокировка (write)
        Shared          // разделяемая блокировка (read)
    }

    /// <summary>
    /// Информация о захваченной блокировке.
    /// </summary>
    public class LockHandle : IDisposable
    {
        private readonly IDistributedLockManager _manager;
        private readonly string _resourceKey;
        private readonly string _lockId;
        private bool _disposed;

        public string ResourceKey => _resourceKey;
        public string LockId => _lockId;
        public DateTime AcquiredAt { get; }
        public static string ForSaga(Guid sagaId) => $"Saga:{sagaId}";

        public LockHandle(IDistributedLockManager manager, string resourceKey, string lockId)
        {
            _manager = manager;
            _resourceKey = resourceKey;
            _lockId = lockId;
            AcquiredAt = DateTime.UtcNow;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _manager.ReleaseAsync(_resourceKey, _lockId).GetAwaiter().GetResult();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Менеджер распределённых блокировок.
    /// </summary>
    public interface IDistributedLockManager
    {
        /// <summary>
        /// Попытаться захватить блокировку ресурса. Возвращает null, если не удалось.
        /// </summary>
        /// <param name="resourceKey">Ключ ресурса (например, "Character:1234").</param>
        /// <param name="mode">Режим блокировки.</param>
        /// <param name="ownerId">Кто захватывает (userId, sessionId).</param>
        /// <param name="timeout">Максимальное время ожидания.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Объект блокировки, или null.</returns>
        Task<LockHandle?> AcquireAsync(string resourceKey, LockMode mode, string ownerId, TimeSpan timeout, CancellationToken cancellationToken = default);

        /// <summary>
        /// Освободить блокировку.
        /// </summary>
        Task ReleaseAsync(string resourceKey, string lockId);

        /// <summary>
        /// Принудительно снять блокировку (только Мастер или администратор).
        /// </summary>
        Task ForceReleaseAsync(string resourceKey, Guid masterUserId);

        /// <summary>
        /// Проверить, удерживается ли блокировка.
        /// </summary>
        Task<bool> IsLockedAsync(string resourceKey);
    }

    /// <summary>
    /// Реализация на основе Redis (StackExchange.Redis).
    /// </summary>
    public class RedisDistributedLockManager : IDistributedLockManager
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisDistributedLockManager> _logger;

        public RedisDistributedLockManager(IConnectionMultiplexer redis, ILogger<RedisDistributedLockManager> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        public async Task<LockHandle?> AcquireAsync(string resourceKey, LockMode mode, string ownerId, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var db = _redis.GetDatabase();
            string lockId = $"{ownerId}:{Guid.NewGuid()}";
            string lockKey = $"lock:{resourceKey}";

            // Для разделяемой блокировки можно использовать Redis счётчик, но для простоты используем только эксклюзивную.
            // В реальном коде можно добавить отдельную реализацию.
            bool acquired = await db.LockTakeAsync(lockKey, lockId, timeout, flags: CommandFlags.None);
            if (acquired)
            {
                _logger.LogInformation("Lock acquired: {ResourceKey} by {OwnerId} (lock {LockId})",
                    resourceKey, ownerId, lockId);
                return new LockHandle(this, resourceKey, lockId);
            }
            _logger.LogDebug("Failed to acquire lock: {ResourceKey} requested by {OwnerId}", resourceKey, ownerId);
            return null;
        }

        public async Task ReleaseAsync(string resourceKey, string lockId)
        {
            var db = _redis.GetDatabase();
            string lockKey = $"lock:{resourceKey}";
            await db.LockReleaseAsync(lockKey, lockId);
            _logger.LogInformation("Lock released: {ResourceKey} ({LockId})", resourceKey, lockId);
        }

        public async Task ForceReleaseAsync(string resourceKey, Guid masterUserId)
        {
            var db = _redis.GetDatabase();
            string lockKey = $"lock:{resourceKey}";
            // Принудительно удаляем ключ (требуются права)
            // В реальной системе – проверка роли мастера
            await db.KeyDeleteAsync(lockKey);
            _logger.LogWarning("Lock force-released: {ResourceKey} by Master {MasterId}", resourceKey, masterUserId);
        }

        public async Task<bool> IsLockedAsync(string resourceKey)
        {
            var db = _redis.GetDatabase();
            return await db.KeyExistsAsync($"lock:{resourceKey}");
        }
    }

    /// <summary>
    /// Фабрика для удобного создания ключей блокировок.
    /// </summary>
    public static class LockKeyFactory
    {
        public static string ForCharacter(Guid characterId) => $"Character:{characterId}";
        public static string ForCombat(Guid combatId) => $"Combat:{combatId}";
        public static string ForCampaign(Guid campaignId) => $"Campaign:{campaignId}";
        public static string ForInventory(Guid characterId) => $"Inventory:{characterId}";
        public static string ForTrade(Guid offerId) => $"Trade:{offerId}";
        public static string ForGlobal(string name) => $"Global:{name}";
        public static string ForSaga(Guid sagaId) => $"Saga:{sagaId}";
    }
}