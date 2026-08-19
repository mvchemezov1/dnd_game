// infrastructure/undo/undo_manager.cs  (объединённая версия)
using System.Collections.Concurrent;
using dnd_game.Application.Security;            // PermissionChecker
using dnd_game.Domain.Commands;
using dnd_game.Domain.Events;
using dnd_game.infrastructure.message_bus;
using Microsoft.Extensions.Logging;

namespace dnd_game.Infrastructure.Undo
{
    // --------------------------------------------------------------------------------
    // Интерфейс действия, поддерживающего Undo / Redo (общий случай)
    // --------------------------------------------------------------------------------
    public interface IUndoableAction
    {
        Guid ActionId { get; }
        DateTime Timestamp { get; }
        Guid UserId { get; }
        Guid GameSessionId { get; }
        string Description { get; }

        Task<bool> CanUndoAsync();
        Task<bool> CanRedoAsync();
        Task UndoAsync();
        Task RedoAsync();
    }

    // --------------------------------------------------------------------------------
    // Абстрактный базовый класс для действий с отменой (на основе IUndoableAction)
    // --------------------------------------------------------------------------------
    public abstract class UndoableActionBase : IUndoableAction
    {
        public Guid ActionId { get; } = Guid.NewGuid();
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public Guid UserId { get; }
        public Guid GameSessionId { get; }
        public abstract string Description { get; }

        protected readonly ICommandBus CommandBus;

        protected UndoableActionBase(Guid userId, Guid gameSessionId, ICommandBus commandBus)
        {
            UserId = userId;
            GameSessionId = gameSessionId;
            CommandBus = commandBus;
        }

        public abstract Task<bool> CanUndoAsync();
        public abstract Task<bool> CanRedoAsync();
        public abstract Task UndoAsync();
        public abstract Task RedoAsync();
    }

    // --------------------------------------------------------------------------------
    // Интерфейс команды, поддерживающей отмену (специализация ICommand)
    // --------------------------------------------------------------------------------
    public interface IUndoableCommand : ICommand
    {
        Guid ExecutionId { get; }
        DateTime ExecutedAt { get; }
        Guid UserId { get; }
        Guid GameSessionId { get; }

        Task<bool> CanUndoAsync();
        Task<bool> CanRedoAsync();
        Task UndoAsync();
        Task RedoAsync();
    }

    // --------------------------------------------------------------------------------
    // Абстрактный базовый класс для команд с отменой (на основе IUndoableCommand)
    // --------------------------------------------------------------------------------
    public abstract class UndoableCommand : IUndoableCommand
    {
        public Guid ExecutionId { get; private set; } = Guid.NewGuid();
        public DateTime ExecutedAt { get; private set; } = DateTime.UtcNow;
        public Guid UserId { get; private set; }
        public Guid GameSessionId { get; private set; }

        protected UndoableCommand(Guid userId, Guid gameSessionId)
        {
            UserId = userId;
            GameSessionId = gameSessionId;
        }

        public abstract Task<bool> CanUndoAsync();
        public abstract Task<bool> CanRedoAsync();
        public abstract Task UndoAsync();
        public abstract Task RedoAsync();
    }

    // --------------------------------------------------------------------------------
    // Менеджер Undo / Redo (единая реализация, заменяет обе версии)
    // --------------------------------------------------------------------------------
    public class UndoManager
    {
        private class SessionUndoState
        {
            public readonly object Lock = new();
            public readonly Stack<IUndoableAction> UndoStack = new();
            public readonly Stack<IUndoableAction> RedoStack = new();
        }

        private readonly ConcurrentDictionary<Guid, SessionUndoState> _sessions = new();
        private readonly ICommandBus _commandBus;
        private readonly PermissionChecker _permissionChecker;
        private readonly ILogger<UndoManager> _logger;

        public int MaxUndoSteps { get; }
        public TimeSpan MaxActionAge { get; }   // действия старше этого срока нельзя отменить

        public UndoManager(
            ICommandBus commandBus,
            PermissionChecker permissionChecker,
            ILogger<UndoManager> logger,
            int maxUndoSteps = 100,
            TimeSpan? maxActionAge = null)
        {
            _commandBus = commandBus;
            _permissionChecker = permissionChecker;
            _logger = logger;
            MaxUndoSteps = maxUndoSteps;
            MaxActionAge = maxActionAge ?? TimeSpan.FromMinutes(30);
        }

        /// <summary>
        /// Зарегистрировать выполненное действие в стеке отмены.
        /// </summary>
        public async Task RecordActionAsync(IUndoableAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var state = _sessions.GetOrAdd(action.GameSessionId, _ => new SessionUndoState());
            lock (state.Lock)
            {
                state.UndoStack.Push(action);
                state.RedoStack.Clear();

                if (state.UndoStack.Count > MaxUndoSteps)
                {
                    var tempList = new List<IUndoableAction>(state.UndoStack);
                    state.UndoStack.Clear();
                    for (int i = Math.Max(0, tempList.Count - MaxUndoSteps); i < tempList.Count; i++)
                        state.UndoStack.Push(tempList[i]);
                }
            }
            _logger.LogDebug("Recorded undo action {ActionId} for session {SessionId}", action.ActionId, action.GameSessionId);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Отменить последнее действие в сессии, если пользователь имеет право.
        /// </summary>
        public async Task<bool> UndoAsync(Guid sessionId, Guid userId)
        {
            if (!_sessions.TryGetValue(sessionId, out var state))
                return false;

            IUndoableAction? action = null;
            lock (state.Lock)
            {
                if (state.UndoStack.Count == 0) return false;
                action = state.UndoStack.Peek();
            }

            if (action!.UserId != userId && !_permissionChecker.IsGameMasterOfCampaign(sessionId))
            {
                _logger.LogWarning("Undo denied for action {ActionId}: user {UserId} lacks permission.", action.ActionId, userId);
                return false;
            }

            if (DateTime.UtcNow - action.Timestamp > MaxActionAge)
            {
                _logger.LogWarning("Undo denied for action {ActionId}: too old ({Age}).", action.ActionId, DateTime.UtcNow - action.Timestamp);
                return false;
            }

            if (!await action.CanUndoAsync())
            {
                _logger.LogWarning("Undo not possible for action {ActionId} at this time.", action.ActionId);
                return false;
            }

            try
            {
                await action.UndoAsync();
                lock (state.Lock)
                {
                    state.UndoStack.Pop();
                    state.RedoStack.Push(action);
                }
                _logger.LogInformation("Undo successful: action {ActionId} by user {UserId}", action.ActionId, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Undo failed for action {ActionId}", action.ActionId);
                throw;
            }
        }

        /// <summary>
        /// Повторить последнее отменённое действие.
        /// </summary>
        public async Task<bool> RedoAsync(Guid sessionId, Guid userId)
        {
            if (!_sessions.TryGetValue(sessionId, out var state))
                return false;

            IUndoableAction? action = null;
            lock (state.Lock)
            {
                if (state.RedoStack.Count == 0) return false;
                action = state.RedoStack.Peek();
            }

            if (action!.UserId != userId && !_permissionChecker.IsGameMasterOfCampaign(sessionId))
            {
                _logger.LogWarning("Redo denied for action {ActionId}: user {UserId} lacks permission.", action.ActionId, userId);
                return false;
            }

            if (!await action.CanRedoAsync())
            {
                _logger.LogWarning("Redo not possible for action {ActionId} at this time.", action.ActionId);
                return false;
            }

            try
            {
                await action.RedoAsync();
                lock (state.Lock)
                {
                    state.RedoStack.Pop();
                    state.UndoStack.Push(action);
                }
                _logger.LogInformation("Redo successful: action {ActionId} by user {UserId}", action.ActionId, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redo failed for action {ActionId}", action.ActionId);
                throw;
            }
        }

        public string? GetLastUndoDescription(Guid sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var state))
            {
                lock (state.Lock)
                {
                    if (state.UndoStack.Count > 0)
                        return state.UndoStack.Peek().Description;
                }
            }
            return null;
        }

        public string? GetLastRedoDescription(Guid sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var state))
            {
                lock (state.Lock)
                {
                    if (state.RedoStack.Count > 0)
                        return state.RedoStack.Peek().Description;
                }
            }
            return null;
        }

        public void ClearSession(Guid sessionId)
        {
            _sessions.TryRemove(sessionId, out _);
            _logger.LogInformation("Undo stacks cleared for session {SessionId}", sessionId);
        }
    }
}