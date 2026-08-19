// infrastructure/coordination/in_memory_lock_manager.cs
using System.Collections.Concurrent;

namespace dnd_game.Infrastructure.Coordination
{
    public class InMemoryLockManager : IDistributedLockManager
    {
        private readonly ConcurrentDictionary<string, (string LockId, DateTime Expiration)> _locks = new();

        public Task<LockHandle?> AcquireAsync(string resourceKey, LockMode mode, string ownerId, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var lockId = $"{ownerId}:{Guid.NewGuid()}";
            var expiration = DateTime.UtcNow + timeout;
            // Только эксклюзивная блокировка для простоты
            if (_locks.TryAdd(resourceKey, (lockId, expiration)))
            {
                return Task.FromResult<LockHandle?>(new LockHandle(this, resourceKey, lockId));
            }
            return Task.FromResult<LockHandle?>(null);
        }

        public Task ReleaseAsync(string resourceKey, string lockId)
        {
            if (_locks.TryGetValue(resourceKey, out var entry) && entry.LockId == lockId)
            {
                _locks.TryRemove(resourceKey, out _);
            }
            return Task.CompletedTask;
        }

        public Task ForceReleaseAsync(string resourceKey, Guid masterUserId)
        {
            _locks.TryRemove(resourceKey, out _);
            return Task.CompletedTask;
        }

        public Task<bool> IsLockedAsync(string resourceKey)
        {
            // Очистка просроченных блокировок (можно добавить)
            return Task.FromResult(_locks.ContainsKey(resourceKey));
        }
    }
}