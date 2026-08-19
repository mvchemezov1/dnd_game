using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using dnd_game.Application.Security;

namespace dnd_game.Infrastructure.Security
{
    // ---------- Интерфейс и модель пользователя ----------
    public interface IUserRepository
    {
        Task<UserAccount?> GetByIdAsync(Guid userId);
        Task<UserAccount?> GetByUsernameAsync(string username);
        Task<UserAccount?> GetByEmailAsync(string email);
        Task AddAsync(UserAccount user);
        Task UpdateAsync(UserAccount user);
        Task DeleteAsync(Guid userId);
    }

    public class UserAccount
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole GlobalRole { get; set; } = UserRole.Player;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        public Dictionary<Guid, CampaignRole> CampaignRoles { get; set; } = new();
    }

    // ---------- Реализация ----------
    public class InMemoryUserRepository : IUserRepository
    {
        private readonly ConcurrentDictionary<Guid, UserAccount> _users = new();
        private readonly ConcurrentDictionary<string, Guid> _usernameIndex = new();
        private readonly ConcurrentDictionary<string, Guid> _emailIndex = new();

        public Task<UserAccount?> GetByIdAsync(Guid userId)
        {
            _users.TryGetValue(userId, out var user);
            return Task.FromResult(user);
        }

        public Task<UserAccount?> GetByUsernameAsync(string username)
        {
            if (_usernameIndex.TryGetValue(username, out var id))
                return GetByIdAsync(id);
            return Task.FromResult<UserAccount?>(null);
        }

        public Task<UserAccount?> GetByEmailAsync(string email)
        {
            if (_emailIndex.TryGetValue(email, out var id))
                return GetByIdAsync(id);
            return Task.FromResult<UserAccount?>(null);
        }

        public Task AddAsync(UserAccount user)
        {
            if (_users.TryAdd(user.Id, user))
            {
                _usernameIndex.TryAdd(user.Username, user.Id);
                _emailIndex.TryAdd(user.Email, user.Id);
            }
            return Task.CompletedTask;
        }

        public Task UpdateAsync(UserAccount user)
        {
            _users.TryUpdate(user.Id, user, user);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid userId)
        {
            if (_users.TryRemove(userId, out var user))
            {
                _usernameIndex.TryRemove(user.Username, out _);
                _emailIndex.TryRemove(user.Email, out _);
            }
            return Task.CompletedTask;
        }
    }
}