// presentation/api/websocket_handler.cs
using dnd_game.Application.Security;
using dnd_game.Domain.Commands;
using dnd_game.Domain.Events;
using dnd_game.Domain.Queries;
using dnd_game.infrastructure.message_bus;
using dnd_game.Infrastructure.MessageBus;
using dnd_game.Infrastructure.Monitoring;
using dnd_game.Infrastructure.Network;
using dnd_game.Infrastructure.Security;
using dnd_game.Infrastructure.Undo;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace dnd_game.Presentation.Api
{
    /// <summary>
    /// Состояние одного WebSocket-подключения.
    /// </summary>
    public class WebSocketConnectionState
    {
        public Guid ConnectionId { get; } = Guid.NewGuid();
        public WebSocket Socket { get; set; } = null!;
        public Guid? UserId { get; set; }
        public Guid? SessionId { get; set; }
        public bool IsAuthenticated => UserId.HasValue;
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Полноценный обработчик WebSocket-соединений, обслуживающий игровые взаимодействия DnD.
    /// </summary>
    public class WebSocketHandler
    {
        private readonly ICommandBus _commandBus;
        private readonly IQueryBus _queryBus;
        private readonly IEventBus _eventBus;
        private readonly ISessionManager _sessionManager;
        private readonly IAuthProvider _authProvider;
        private readonly INetworkProtocol _protocol;
        private readonly PermissionChecker _permissionChecker;
        private readonly IMetricsCollector _metricsCollector;
        private readonly ITracer _tracer;
        private readonly IRateLimiter _rateLimiter;
        private readonly ILogger<WebSocketHandler> _logger;
        private readonly UndoManager _undoManager;

        // Активные подключения
        private readonly ConcurrentDictionary<Guid, WebSocketConnectionState> _connections = new();

        // Подписки на события: ConnectionId -> список отписок (чтобы можно было отписаться при дисконнекте)
        private readonly ConcurrentDictionary<Guid, List<Action>> _eventSubscriptions = new();

        private readonly int _receiveBufferSize = 4096;
        private readonly TimeSpan _keepAliveInterval = TimeSpan.FromSeconds(30);

        public WebSocketHandler(
            ICommandBus commandBus,
            IQueryBus queryBus,
            IEventBus eventBus,
            ISessionManager sessionManager,
            IAuthProvider authProvider,
            INetworkProtocol protocol,
            PermissionChecker permissionChecker,
            IMetricsCollector metricsCollector,
            ITracer tracer,
            IRateLimiter rateLimiter,
            ILogger<WebSocketHandler> logger,
            UndoManager undoManager)
        {
            _commandBus = commandBus;
            _queryBus = queryBus;
            _eventBus = eventBus;
            _sessionManager = sessionManager;
            _authProvider = authProvider;
            _protocol = protocol;
            _permissionChecker = permissionChecker;
            _metricsCollector = metricsCollector;
            _tracer = tracer;
            _rateLimiter = rateLimiter;
            _logger = logger;
            _undoManager = undoManager;
        }

        /// <summary>
        /// Р”РµС‚РµСЂРјРёРЅРёСЂРѕРІР°РЅРЅРѕ РїСЂРµРІСЂР°С‰Р°РµС‚ IP-Р°РґСЂРµСЃ РІ Guid РґР»СЏ РёСЃРїРѕР»СЊР·РѕРІР°РЅРёСЏ РєР°Рє РєР»СЋС‡ РєР»РёРµРЅС‚Р°
        /// РІ IRateLimiter (РєРѕС‚РѕСЂС‹Р№ Р°РґСЂРµСЃСѓРµС‚СЃСЏ РїРѕ Guid). РћРґРёРЅР°РєРѕРІС‹Р№ IP РІСЃРµРіРґР° РґР°С‘С‚ РѕРґРёРЅ Рё С‚РѕС‚ Р¶Рµ Guid,
        /// С‡С‚Рѕ Рё РЅСѓР¶РЅРѕ РґР»СЏ РїРѕРґСЃС‡С‘С‚Р° РєРѕР»РёС‡РµСЃС‚РІР° РїРѕРґРєР»СЋС‡РµРЅРёР№ СЃ РѕРґРЅРѕРіРѕ Р°РґСЂРµСЃР°.
        /// </summary>
        private static Guid IpToClientId(IPAddress? ip)
        {
            if (ip == null) return Guid.Empty;
            var hash = MD5.HashData(ip.GetAddressBytes());
            return new Guid(hash);
        }

        /// <summary>
        /// Основной метод обработки WebSocket-соединения. Вызывается из middleware.
        /// </summary>
        /// <summary>
        /// Основной метод обработки WebSocket-соединения.
        /// </summary>
        public async Task HandleAsync(
            WebSocket socket,
            HttpContext httpContext,
            CancellationToken cancellationToken,
            IPAddress? remoteIp = null)
        {
            var state = new WebSocketConnectionState { Socket = socket, ConnectedAt = DateTime.UtcNow };

            // Ограничение количества подключений с одного IP
            if (!_rateLimiter.IsAllowed(IpToClientId(remoteIp), "websocket-connect"))
            {
                _logger.LogWarning("WebSocket connection rejected due to rate limit for IP {RemoteIp}", remoteIp);
                await CloseConnection(state, WebSocketCloseStatus.PolicyViolation, "Too many connection attempts");
                return;
            }

            _connections[state.ConnectionId] = state;
            _logger.LogInformation("WebSocket connection {ConnectionId} opened", state.ConnectionId);

            try
            {
                // ===== Аутентификация через токен в query-строке =====
                var token = httpContext.Request.Query["token"].FirstOrDefault();
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("WebSocket connection {ConnectionId} missing token", state.ConnectionId);
                    await SendErrorMessage(state, "AUTH_REQUIRED", "Authentication required. Provide token in query string.");
                    await CloseConnection(state, WebSocketCloseStatus.PolicyViolation, "Authentication failed");
                    return;
                }

                var userContext = await _authProvider.GetUserContextFromTokenAsync(token);
                if (userContext == null)
                {
                    _logger.LogWarning("WebSocket connection {ConnectionId} invalid token", state.ConnectionId);
                    await SendErrorMessage(state, "AUTH_FAILED", "Invalid or expired token.");
                    await CloseConnection(state, WebSocketCloseStatus.PolicyViolation, "Authentication failed");
                    return;
                }

                state.UserId = userContext.UserId;
                _logger.LogInformation("WebSocket connection {ConnectionId} authenticated as user {UserId}", state.ConnectionId, state.UserId);

                // Если клиент передал sessionId, привязываем
                var sessionIdQuery = httpContext.Request.Query["sessionId"].FirstOrDefault();
                if (Guid.TryParse(sessionIdQuery, out var sessionId))
                {
                    state.SessionId = sessionId;
                    await _sessionManager.AssociateConnection(state.UserId.Value, sessionId, state.ConnectionId);
                }

                // ===== Конец аутентификации =====

                // Подписка на события сессии
                SubscribeToEvents(state, cancellationToken);

                // Keep-alive
                var keepAliveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var keepAliveTask = Task.Run(() => KeepAliveLoopAsync(state, keepAliveCts.Token));

                // Основной цикл приёма сообщений (теперь аутентифицирован)
                await ReceiveLoopAsync(state, cancellationToken);

                keepAliveCts.Cancel();
                try { await keepAliveTask; } catch { /* игнорируем */ }
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning(ex, "WebSocket error for connection {ConnectionId}", state.ConnectionId);
            }
            catch (OperationCanceledException)
            {
                // нормальное завершение
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in WebSocket connection {ConnectionId}", state.ConnectionId);
            }
            finally
            {
                await CloseConnection(state, WebSocketCloseStatus.NormalClosure, "Server closing");
            }
        }

        /// <summary>
        /// Вспомогательный метод для отправки сообщения об ошибке.
        /// </summary>
        private async Task SendErrorMessage(WebSocketConnectionState state, string errorCode, string message)
        {
            var errorMsg = new ErrorNetworkMessage
            {
                ErrorCode = errorCode,
                Message = message
            };
            await SendMessageAsync(state, errorMsg);
        }

        // ---------- Аутентификация ----------
        private async Task<bool> AuthenticateAsync(WebSocketConnectionState state, CancellationToken cancellationToken)
        {
            var authTimeout = TimeSpan.FromSeconds(15);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(authTimeout);

            WebSocketReceiveResult result;
            byte[] rawMessage;
            try
            {
                (result, rawMessage) = await ReceiveFullMessageAsync(state.Socket, cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Authentication timeout for connection {ConnectionId}", state.ConnectionId);
                return false;
            }
            if (result.MessageType == WebSocketMessageType.Close)
                return false;

            var message = DecodeMessage(rawMessage);
            if (message == null || message.Type != MessageType.AuthRequest)
            {
                await SendMessageAsync(state, NetworkMessageFactory.CreateError("AUTH_REQUIRED", "Authentication required."));
                return false;
            }

            var authMsg = DeserializePayload<AuthRequestMessage>(message);
            if (authMsg == null || string.IsNullOrWhiteSpace(authMsg.Token))
            {
                await SendMessageAsync(state, NetworkMessageFactory.CreateError("AUTH_INVALID", "Invalid token."));
                return false;
            }

            var userContext = await _authProvider.GetUserContextFromTokenAsync(authMsg.Token);
            if (userContext == null)
            {
                await SendMessageAsync(state, NetworkMessageFactory.CreateError("AUTH_FAILED", "Token validation failed."));
                return false;
            }

            state.UserId = userContext.UserId;

            // Привязка к сессии: если клиент не передал SessionId, можно попытаться определить из токена или оставить без сессии
            Guid sessionId = Guid.Empty;
            if (message is IHasSessionId sessionMsg)
                sessionId = sessionMsg.SessionId;

            state.SessionId = sessionId != Guid.Empty ? sessionId : null;
            if (state.SessionId.HasValue)
            {
                await _sessionManager.AssociateConnection(state.UserId.Value, state.SessionId.Value, state.ConnectionId);
            }

            await SendMessageAsync(state, new AuthResponseMessage { Success = true, UserId = userContext.UserId });
            _logger.LogInformation("User {UserId} authenticated on connection {ConnectionId}", state.UserId, state.ConnectionId);
            return true;
        }

        // ---------- Основной цикл приёма ----------
        private async Task ReceiveLoopAsync(WebSocketConnectionState state, CancellationToken cancellationToken)
        {
            var buffer = new byte[_receiveBufferSize];
            while (state.Socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                byte[] rawMessage;
                try
                {
                    (result, rawMessage) = await ReceiveFullMessageAsync(state.Socket, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                state.LastMessageAt = DateTime.UtcNow;

                // Лимит сообщений
                var rateLimitClientId = state.UserId ?? state.ConnectionId;
                if (!_rateLimiter.IsAllowed(rateLimitClientId, "websocket-message"))
                {
                    await SendMessageAsync(state, NetworkMessageFactory.CreateError("RATE_LIMITED", "Too many messages, slow down."));
                    continue;
                }

                // Пытаемся декодировать как JSON
                INetworkMessage? networkMsg = null;
                try
                {
                    var json = Encoding.UTF8.GetString(rawMessage);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("type", out var typeElement))
                    {
                        var type = typeElement.GetString();
                        networkMsg = type switch
                        {
                            "command" => JsonSerializer.Deserialize<CommandNetworkMessage>(json),
                            "query" => JsonSerializer.Deserialize<QueryNetworkMessage>(json),
                            "ping" => JsonSerializer.Deserialize<Infrastructure.Network.PingMessage>(json),
                            "undo_request" => JsonSerializer.Deserialize<UndoNetworkMessage>(json),
                            "redo_request" => JsonSerializer.Deserialize<RedoNetworkMessage>(json),
                            "chat" => JsonSerializer.Deserialize<ChatNetworkMessage>(json),
                            _ => null
                        };
                        if (networkMsg == null)
                        {
                            _logger.LogWarning("Unknown message type: {Type}", type);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to decode JSON message: {Raw}", Encoding.UTF8.GetString(rawMessage));
                }

                // Если не JSON – пробуем бинарный протокол
                if (networkMsg == null)
                {
                    var decoded = _protocol.Decode(rawMessage);
                    networkMsg = decoded.FirstOrDefault();
                }

                if (networkMsg == null)
                {
                    _logger.LogWarning("Failed to decode message: {Raw}", Encoding.UTF8.GetString(rawMessage));
                    continue;
                }

                _logger.LogDebug("Received message type: {Type}", networkMsg.Type);

                using var span = _tracer.StartSpan("WebSocket.Message");
                _tracer.SetTag("connection.id", state.ConnectionId.ToString());
                _tracer.SetTag("message.type", networkMsg.Type.ToString());

                try
                {
                    await DispatchMessageAsync(state, networkMsg);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error dispatching message type {MessageType}", networkMsg.Type);
                    await SendMessageAsync(state, NetworkMessageFactory.CreateError("PROCESSING_ERROR", ex.Message));
                }
            }
        }

        // ---------- Диспетчеризация сообщений ----------

        private async Task HandleCommandMessage(WebSocketConnectionState state, CommandNetworkMessage msg)
        {
            var commandType = Type.GetType(msg.CommandTypeName);
            if (commandType == null)
            {
                await SendMessageAsync(state, NetworkMessageFactory.CreateError("UNKNOWN_COMMAND", "Command type not found."));
                return;
            }

            var commandObj = JsonSerializer.Deserialize(msg.CommandJson, commandType) as ICommand;
            if (commandObj == null)
            {
                await SendMessageAsync(state, NetworkMessageFactory.CreateError("DESERIALIZE_ERROR", "Invalid command payload."));
                return;
            }

            var context = new CommandContext
            {
                UserId = state.UserId ?? Guid.Empty,
                GameSessionId = state.SessionId ?? Guid.Empty,
                CancellationToken = CancellationToken.None
            };

            await _commandBus.SendAsync(commandObj, context);

            // Отправляем подтверждение успеха, если требуется
            await SendMessageAsync(state, new CommandResponseNetworkMessage { Success = true, CorrelationId = msg.CorrelationId });
        }

        private async Task HandleQueryMessage(WebSocketConnectionState state, QueryNetworkMessage msg)
        {
            // Аналогично командам, но через IQueryBus
            var queryType = Type.GetType(msg.QueryTypeName);
            if (queryType == null)
            {
                await SendMessageAsync(state, NetworkMessageFactory.CreateError("UNKNOWN_QUERY", "Query type not found."));
                return;
            }

            var queryObj = JsonSerializer.Deserialize(msg.QueryJson, queryType);
            // Здесь необходимо вызвать QueryAsync с использованием рефлексии, т.к. IQueryBus параметризован.
            // Для упрощения можно использовать динамический вызов через IQueryBus, который принимает object?
            // Но в нашей реализации IQueryBus метод QueryAsync<TResult>.
            // Поэтому потребуется reflection. Опускаем детали для краткости, оставляем заглушку.
            await SendMessageAsync(state, NetworkMessageFactory.CreateError("NOT_IMPLEMENTED", "WebSocket queries not yet supported."));
        }

        // ---------- Вспомогательные методы ----------
        private async Task<(WebSocketReceiveResult result, byte[] data)> ReceiveFullMessageAsync(WebSocket socket, CancellationToken cancellationToken)
        {
            using var ms = new MemoryStream();
            var buffer = new byte[_receiveBufferSize];
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                    return (result, Array.Empty<byte>());
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            return (result, ms.ToArray());
        }

        private INetworkMessage? DecodeMessage(byte[] data)
        {
            var decoded = _protocol.Decode(data);
            return decoded.FirstOrDefault();
        }

        private async Task SendMessageAsync(WebSocketConnectionState state, INetworkMessage message)
        {
            if (state.Socket.State != WebSocketState.Open) return;
            var bytes = _protocol.Encode(message);
            var segment = new ArraySegment<byte>(bytes);
            try
            {
                await state.Socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send message to connection {ConnectionId}", state.ConnectionId);
            }
        }

        private T? DeserializePayload<T>(INetworkMessage message) where T : class
        {
            // В зависимости от реализации протокола, может потребоваться десериализовать Payload.
            // В нашем NetworkMessage типы CommandNetworkMessage и т.д. уже содержат JSON строку.
            if (message is CommandNetworkMessage cmdMsg)
                return JsonSerializer.Deserialize<T>(cmdMsg.CommandJson);
            if (message is EventNetworkMessage eventMsg)
                return JsonSerializer.Deserialize<T>(eventMsg.EventJson);
            if (message is AuthRequestMessage authReq)
                // AuthRequestMessage сам является типом
                return authReq as T;
            return null;
        }

        // ---------- Keep-alive пинг ----------
        private async Task KeepAliveLoopAsync(WebSocketConnectionState state, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && state.Socket.State == WebSocketState.Open)
            {
                await Task.Delay(_keepAliveInterval, cancellationToken);
                if (state.Socket.State == WebSocketState.Open)
                {
                    try
                    {
                        await state.Socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("ping")), WebSocketMessageType.Text, true, cancellationToken);
                    }
                    catch
                    {
                        break;
                    }
                }
            }
        }

        // ---------- Закрытие соединения ----------
        private async Task CloseConnection(WebSocketConnectionState state, WebSocketCloseStatus status, string description)
        {
            if (state.Socket.State == WebSocketState.Open)
            {
                try
                {
                    await state.Socket.CloseAsync(status, description, CancellationToken.None);
                }
                catch { }
            }

            // Отписка от событий
            if (_eventSubscriptions.TryRemove(state.ConnectionId, out var unsubList))
            {
                foreach (var unsub in unsubList) unsub();
            }

            _sessionManager.RemoveConnection(state.ConnectionId);
            _connections.TryRemove(state.ConnectionId, out _);
            _metricsCollector.IncrementCounter("dnd.websocket.disconnected");
            _logger.LogInformation("WebSocket connection {ConnectionId} closed ({Status})", state.ConnectionId, status);
        }



        /// <summary>
        /// Основной метод обработки WebSocket-соединения.
        /// </summary>

        // ---------- Обработка входящих сообщений ----------
        private async Task DispatchMessageAsync(WebSocketConnectionState state, INetworkMessage networkMsg)
        {
            _logger.LogDebug("Dispatching message type: {Type}", networkMsg.Type);
            switch (networkMsg)
            {
                case CommandNetworkMessage cmd:
                    await HandleCommand(state, cmd);
                    break;
                case QueryNetworkMessage query:
                    await HandleQuery(state, query);
                    break;
                case Infrastructure.Network.PingMessage ping:
                    await SendMessageAsync(state, new PongMessage());
                    break;
                case UndoNetworkMessage undo:
                    await HandleUndo(state, undo);
                    break;
                case RedoNetworkMessage redo:
                    await HandleRedo(state, redo);
                    break;
                case ChatNetworkMessage chat:
                    await SendMessageAsync(state, new ChatResponseNetworkMessage
                    {
                        Payload = "Echo: " + chat.Message,
                        CorrelationId = chat.CorrelationId
                    });
                    break;
                default:
                    _logger.LogWarning("Unknown message type: {Type}", networkMsg.Type);
                    await SendMessageAsync(state, new ErrorNetworkMessage
                    {
                        ErrorCode = "UNKNOWN_TYPE",
                        Message = $"Unsupported message type: {networkMsg.Type}"
                    });
                    break;
            }
        }

        private async Task HandleUndo(WebSocketConnectionState state, UndoNetworkMessage msg)
        {
            if (!state.UserId.HasValue || !state.SessionId.HasValue)
            {
                await SendMessageAsync(state, new UndoResponseNetworkMessage
                {
                    Success = false,
                    ErrorMessage = "Not authenticated or no session.",
                    CorrelationId = msg.CorrelationId
                });
                return;
            }

            try
            {
                var success = await _undoManager.UndoAsync(state.SessionId.Value, state.UserId.Value);
                await SendMessageAsync(state, new UndoResponseNetworkMessage
                {
                    Success = success,
                    CorrelationId = msg.CorrelationId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Undo failed for session {SessionId}", state.SessionId);
                await SendMessageAsync(state, new UndoResponseNetworkMessage
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    CorrelationId = msg.CorrelationId
                });
            }
        }

        private async Task HandleRedo(WebSocketConnectionState state, RedoNetworkMessage msg)
        {
            if (!state.UserId.HasValue || !state.SessionId.HasValue)
            {
                await SendMessageAsync(state, new RedoResponseNetworkMessage
                {
                    Success = false,
                    ErrorMessage = "Not authenticated or no session.",
                    CorrelationId = msg.CorrelationId
                });
                return;
            }

            try
            {
                var success = await _undoManager.RedoAsync(state.SessionId.Value, state.UserId.Value);
                await SendMessageAsync(state, new RedoResponseNetworkMessage
                {
                    Success = success,
                    CorrelationId = msg.CorrelationId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redo failed for session {SessionId}", state.SessionId);
                await SendMessageAsync(state, new RedoResponseNetworkMessage
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    CorrelationId = msg.CorrelationId
                });
            }
        }

        private async Task HandleCommand(WebSocketConnectionState state, CommandNetworkMessage msg)
        {
            var commandType = Type.GetType(msg.CommandTypeName);
            if (commandType == null)
            {
                await SendMessageAsync(state, NetworkMessageFactory.CreateError("UNKNOWN_COMMAND", "Command type not found."));
                return;
            }

            var commandObj = JsonSerializer.Deserialize(msg.CommandJson, commandType) as ICommand;
            if (commandObj == null)
            {
                await SendMessageAsync(state, NetworkMessageFactory.CreateError("DESERIALIZE_ERROR", "Invalid command payload."));
                return;
            }

            var context = new CommandContext
            {
                UserId = state.UserId ?? Guid.Empty,
                GameSessionId = state.SessionId ?? Guid.Empty,
                CancellationToken = CancellationToken.None
            };

            try
            {
                await _commandBus.SendAsync(commandObj, context);
                if (commandObj is IUndoableAction undoableAction)
                {
                    await _undoManager.RecordActionAsync(undoableAction);
                }
                _logger.LogInformation("Command {CommandType} executed successfully", commandType.Name);
                await SendMessageAsync(state, new CommandResponseNetworkMessage { Success = true, CorrelationId = msg.CorrelationId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Command {CommandType} failed", commandType.Name);
                await SendMessageAsync(state, NetworkMessageFactory.CreateError("COMMAND_FAILED", ex.Message, correlationId: msg.CorrelationId));
            }
        }

        /// <summary>
        /// Обработка входящего запроса (Query) через WebSocket.
        /// </summary>
        private async Task HandleQuery(WebSocketConnectionState state, QueryNetworkMessage msg)
        {
            try
            {
                // 1. Загружаем тип запроса
                var queryType = Type.GetType(msg.QueryTypeName);
                if (queryType == null)
                {
                    await SendMessageAsync(state, new QueryResponseNetworkMessage
                    {
                        Success = false,
                        ErrorMessage = $"Unknown query type: {msg.QueryTypeName}",
                        CorrelationId = msg.CorrelationId
                    });
                    return;
                }

                // 2. Десериализуем запрос
                object? queryObj = JsonSerializer.Deserialize(msg.QueryJson, queryType);
                if (queryObj == null)
                {
                    await SendMessageAsync(state, new QueryResponseNetworkMessage
                    {
                        Success = false,
                        ErrorMessage = "Failed to deserialize query payload.",
                        CorrelationId = msg.CorrelationId
                    });
                    return;
                }

                // 3. Вызываем IQueryBus.QueryAsync через рефлексию
                //    Сигнатура: Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, QueryContext? context = null, CancellationToken cancellationToken = default)
                var queryBusType = typeof(IQueryBus);
                var method = queryBusType.GetMethod("QueryAsync");
                if (method == null)
                {
                    await SendMessageAsync(state, new QueryResponseNetworkMessage
                    {
                        Success = false,
                        ErrorMessage = "QueryAsync method not found on IQueryBus.",
                        CorrelationId = msg.CorrelationId
                    });
                    return;
                }

                // Определяем тип результата: IQuery<TResult> -> TResult
                var queryInterface = queryType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>));
                if (queryInterface == null)
                {
                    await SendMessageAsync(state, new QueryResponseNetworkMessage
                    {
                        Success = false,
                        ErrorMessage = $"Type {queryType.Name} does not implement IQuery<TResult>.",
                        CorrelationId = msg.CorrelationId
                    });
                    return;
                }

                var resultType = queryInterface.GetGenericArguments()[0];
                var genericMethod = method.MakeGenericMethod(resultType);

                // Подготавливаем аргументы: query, context (может быть null), cancellationToken
                var context = new QueryContext
                {
                    UserId = state.UserId ?? Guid.Empty,
                    GameSessionId = state.SessionId ?? Guid.Empty
                };
                object?[] parameters = { queryObj, context, CancellationToken.None };

                // 4. Выполняем запрос
                var task = (Task)genericMethod.Invoke(_queryBus, parameters)!;
                await task.ConfigureAwait(false);

                // 5. Получаем результат через свойство Result (т.к. это Task<T>)
                var resultProperty = task.GetType().GetProperty("Result");
                if (resultProperty == null)
                {
                    await SendMessageAsync(state, new QueryResponseNetworkMessage
                    {
                        Success = false,
                        ErrorMessage = "Failed to retrieve query result.",
                        CorrelationId = msg.CorrelationId
                    });
                    return;
                }
                var result = resultProperty.GetValue(task);

                // 6. Сериализуем результат в JSON
                string? resultJson = result != null ? JsonSerializer.Serialize(result, resultType) : null;

                // 7. Отправляем успешный ответ
                await SendMessageAsync(state, new QueryResponseNetworkMessage
                {
                    Success = true,
                    ResultJson = resultJson,
                    CorrelationId = msg.CorrelationId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing query {QueryTypeName}", msg.QueryTypeName);
                await SendMessageAsync(state, new QueryResponseNetworkMessage
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}",
                    CorrelationId = msg.CorrelationId
                });
            }
        }

        // ---------- Подписка на события и рассылка ----------
        private void SubscribeToEvents(WebSocketConnectionState state, CancellationToken cancellationToken)
        {
            if (!state.SessionId.HasValue) return;

            async Task EventHandler(IDomainEvent @event, CancellationToken ct)
            {
                if (state.Socket.State != WebSocketState.Open) return;
                if (!ShouldSendEventToSession(@event, state.SessionId.Value)) return;

                var eventMsg = NetworkMessageFactory.FromEvent(@event);
                await SendMessageAsync(state, eventMsg);
            }

            _eventBus.Subscribe<IDomainEvent>(EventHandler);
            // Сохраняем отписку для закрытия соединения
            _eventSubscriptions.AddOrUpdate(state.ConnectionId, _ => new List<Action> { () => _eventBus.Subscribe<IDomainEvent>((Func<IDomainEvent, CancellationToken, Task>)EventHandler) },
                                             (_, list) => { list.Add(() => _eventBus.Subscribe<IDomainEvent>((Func<IDomainEvent, CancellationToken, Task>)EventHandler)); return list; });
        }

        private bool ShouldSendEventToSession(IDomainEvent @event, Guid sessionId)
        {
            // Проверяем, относится ли событие к сессии (например, через ISessionBoundEvent)
            if (@event is ISessionBoundEvent sessionEvent)
                return sessionEvent.GameSessionId == sessionId;
            // Если событие не содержит сессию – отправляем всем клиентам сессии
            return true;
        }
    }
}