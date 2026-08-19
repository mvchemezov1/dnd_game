// presentation/dm_tools/undo_manager.cs
using dnd_game.Domain.Commands;
using dnd_game.infrastructure.message_bus;
using dnd_game.Infrastructure.Undo;
using Microsoft.Extensions.Logging;

namespace dnd_game.Presentation.DmTools
{
    /// <summary>
    /// Инструмент Мастера для управления отменой и повтором действий.
    /// Обёртка над UndoManager из инфраструктуры, адаптированная для DM UI.
    /// </summary>
    public class DmUndoManager
    {
        private readonly UndoManager _undoManager;
        private readonly ICommandBus _commandBus;
        private readonly ILogger<DmUndoManager> _logger;

        public DmUndoManager(UndoManager undoManager, ICommandBus commandBus, ILogger<DmUndoManager> logger)
        {
            _undoManager = undoManager;
            _commandBus = commandBus;
            _logger = logger;
        }

        /// <summary>
        /// Выполнить команду и зарегистрировать её в стеке отмены, если она поддерживает отмену.
        /// </summary>
        public async Task RecordAndExecute(ICommand command, Guid userId, Guid sessionId)
        {
            // Выполняем команду
            await _commandBus.SendAsync(command, new CommandContext { UserId = userId, GameSessionId = sessionId });

            // Если команда реализует IUndoableAction – регистрируем её.
            if (command is IUndoableAction action)
            {
                await _undoManager.RecordActionAsync(action);
            }
            else
            {
                // Для произвольных команд создаём базовую обёртку, где Undo/Redo недоступны.
                // В реальном приложении здесь может быть маппинг обратных команд.
                _logger.LogDebug("Command {CommandType} does not implement IUndoableAction; undo not recorded.", command.GetType().Name);
            }
        }

        /// <summary>
        /// Отменить последнее действие в сессии (если разрешено).
        /// </summary>
        public async Task<bool> Undo(Guid sessionId, Guid userId)
        {
            return await _undoManager.UndoAsync(sessionId, userId);
        }

        /// <summary>
        /// Повторить последнее отменённое действие.
        /// </summary>
        public async Task<bool> Redo(Guid sessionId, Guid userId)
        {
            return await _undoManager.RedoAsync(sessionId, userId);
        }

        /// <summary>
        /// Получить описание последнего действия, доступного для отмены (для UI).
        /// </summary>
        public string? GetLastUndoDescription(Guid sessionId)
        {
            return _undoManager.GetLastUndoDescription(sessionId);
        }

        /// <summary>
        /// Получить описание последнего действия, доступного для повтора.
        /// </summary>
        public string? GetLastRedoDescription(Guid sessionId)
        {
            return _undoManager.GetLastRedoDescription(sessionId);
        }

        /// <summary>
        /// Очистить историю отмены/повтора для сессии (например, при смене сцены).
        /// </summary>
        public void ClearSessionHistory(Guid sessionId)
        {
            _undoManager.ClearSession(sessionId);
        }

        /// <summary>
        /// Отменить конкретное действие по его идентификатору (если поддерживается).
        /// </summary>
        public async Task<bool> UndoActionById(Guid sessionId, Guid actionId, Guid userId)
        {
            // Данный метод требует доступа к внутреннему стеку. В UndoManager он отсутствует.
            // Можно реализовать поиск и вытаскивание из стека, но это сложно.
            // Пока оставим как заглушку.
            _logger.LogWarning("Undo by action ID not implemented.");
            await Task.CompletedTask;
            return false;
        }

        /// <summary>
        /// GM принудительно отменяет последнее действие любого игрока в сессии.
        /// </summary>
        public async Task<bool> ForceUndoLastPlayerAction(Guid sessionId, Guid gmUserId)
        {
            // Проверяем, является ли пользователь GM (PermissionChecker должен быть вызван раньше,
            // но мы доверяем контексту DM-инструментов). Вызываем обычный Undo от имени GM,
            // который внутри проверяет права: если GM, то разрешено.
            return await _undoManager.UndoAsync(sessionId, gmUserId);
        }
    }
}