// infrastructure/monitoring/logging_middleware.cs
using dnd_game.Domain.Commands;
using dnd_game.Domain.Queries;
using dnd_game.Domain.Events;
using dnd_game.Infrastructure.MessageBus;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using dnd_game.infrastructure.message_bus;

namespace dnd_game.Infrastructure.Monitoring
{
    /// <summary>
    /// Конфигурация уровней логирования для middleware.
    /// </summary>
    public enum MiddlewareLogLevel
    {
        Minimal,   // только критические ошибки
        Normal,    // информация о командах/запросах и ошибках
        Verbose    // все детали, включая параметры
    }

    /// <summary>
    /// Middleware, реализующий сквозное логирование для всех команд, запросов и событий.
    /// Выступает как ICommandPipelineBehavior и IQueryPipelineBehavior для вставки в шины.
    /// Также может быть использован как обработчик событий для логирования доменных событий.
    /// </summary>
    public class LoggingMiddleware : ICommandPipelineBehavior, IQueryPipelineBehavior
    {
        private readonly ILogger<LoggingMiddleware> _logger;
        private readonly MiddlewareLogLevel _logLevel;

        public LoggingMiddleware(ILogger<LoggingMiddleware> logger, MiddlewareLogLevel logLevel = MiddlewareLogLevel.Normal)
        {
            _logger = logger;
            _logLevel = logLevel;
        }

        // ---------- Команды ----------
        public async Task HandleAsync<TCommand>(TCommand command, CommandContext context, Func<Task> next) where TCommand : ICommand
        {
            var commandType = typeof(TCommand).Name;
            var userId = context?.UserId ?? Guid.Empty;
            var sessionId = context?.GameSessionId ?? Guid.Empty;

            if (_logLevel >= MiddlewareLogLevel.Normal)
            {
                _logger.LogInformation(
                    "▶ Command {CommandType} started | User={UserId} Session={SessionId}",
                    commandType, userId, sessionId);
            }

            if (_logLevel >= MiddlewareLogLevel.Verbose)
            {
                _logger.LogDebug("Command payload: {@Command}", command);
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await next();

                stopwatch.Stop();
                if (_logLevel >= MiddlewareLogLevel.Normal)
                {
                    _logger.LogInformation(
                        "✓ Command {CommandType} completed in {ElapsedMs}ms",
                        commandType, stopwatch.ElapsedMilliseconds);
                }

                // Метрика времени выполнения (можно отправить в MetricsCollector)
                // Здесь просто логируем
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex,
                    "✕ Command {CommandType} failed after {ElapsedMs}ms | User={UserId} Session={SessionId}",
                    commandType, stopwatch.ElapsedMilliseconds, userId, sessionId);
                throw; // пробрасываем исключение дальше
            }
        }

        // ---------- Запросы ----------
        public async Task<TResult> HandleAsync<TResult>(IQuery<TResult> query, QueryContext context, Func<Task<TResult>> next)
        {
            var queryType = query.GetType().Name;
            var userId = context?.UserId ?? Guid.Empty;
            var sessionId = context?.GameSessionId ?? Guid.Empty;

            if (_logLevel >= MiddlewareLogLevel.Normal)
            {
                _logger.LogInformation(
                    "Ⓠ Query {QueryType} started | User={UserId} Session={SessionId}",
                    queryType, userId, sessionId);
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = await next();

                stopwatch.Stop();
                if (_logLevel >= MiddlewareLogLevel.Normal)
                {
                    _logger.LogInformation(
                        "✓ Query {QueryType} completed in {ElapsedMs}ms",
                        queryType, stopwatch.ElapsedMilliseconds);
                }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex,
                    "✕ Query {QueryType} failed after {ElapsedMs}ms | User={UserId} Session={SessionId}",
                    queryType, stopwatch.ElapsedMilliseconds, userId, sessionId);
                throw;
            }
        }

        // ---------- События (опционально: можно использовать как IEventHandler<IDomainEvent>) ----------
        /// <summary>
        /// Обрабатывает доменное событие для логирования. Может быть зарегистрирован как IEventHandler(IDomainEvent).
        /// </summary>
        public Task HandleEvent(IDomainEvent @event, CancellationToken cancellationToken = default)
        {
            if (_logLevel >= MiddlewareLogLevel.Verbose)
            {
                _logger.LogDebug("Event {EventType}: {@Event}", @event.GetType().Name, @event);
            }
            else if (_logLevel >= MiddlewareLogLevel.Normal)
            {
                // События не логируем на Normal уровне, чтобы не засорять лог, кроме важных.
                // Но можно сделать условное логирование для определённых типов (смерть, бой и т.д.)
                if (@event is CharacterDied or CombatStarted or QuestCompleted)
                {
                    _logger.LogInformation("⚡ Important event: {EventType} | Data: {@Event}", @event.GetType().Name, @event);
                }
            }
            return Task.CompletedTask;
        }
    }
}