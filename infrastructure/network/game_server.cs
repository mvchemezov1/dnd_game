// infrastructure/network/game_server.cs
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using dnd_game.Domain.Commands;
using dnd_game.Domain.Events;
using dnd_game.Application.Security;
using dnd_game.Infrastructure.MessageBus;
using dnd_game.Infrastructure.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using dnd_game.infrastructure.message_bus;

namespace dnd_game.Infrastructure.Network
{
    /// <summary>
    /// Статус подключения клиента.
    /// </summary>
    public enum ConnectionState
    {
        Connecting,
        Authenticated,
        Disconnecting,
        Disconnected
    }

    /// <summary>
    /// Протокол передачи данных.
    /// </summary>
    public enum TransportProtocol
    {
        WebSocket,
        Tcp
    }

    /// <summary>
    /// Интерфейс клиентского подключения (абстрагирует WebSocket и TCP).
    /// </summary>
    public interface IClientConnection
    {
        Guid ConnectionId { get; }
        Guid? UserId { get; set; }
        Guid? SessionId { get; set; }
        ConnectionState State { get; set; }
        TransportProtocol Protocol { get; }
        Task SendAsync(ArraySegment<byte> data, CancellationToken cancellationToken);
        Task CloseAsync(CancellationToken cancellationToken);
        event Func<IClientConnection, byte[], Task>? MessageReceived;
    }

    /// <summary>
    /// Менеджер игровых сессий (сопоставление UserId -> SessionId, управление ролями).
    /// </summary>
    public interface ISessionManager
    {
        Task<Guid> CreateSession(Guid userId, string campaignId);
        Task JoinSession(Guid sessionId, Guid userId);
        Task LeaveSession(Guid sessionId, Guid userId);
        Task<bool> IsUserInSession(Guid userId, Guid sessionId);
        Task<IEnumerable<Guid>> GetSessionUsers(Guid sessionId);
        Task<CampaignRole?> GetUserRole(Guid userId, Guid sessionId);
        Task AssociateConnection(Guid userId, Guid sessionId, Guid connectionId);
        void RemoveConnection(Guid connectionId);
    }

    /// <summary>
    /// Сообщение сетевого протокола (JSON).
    /// </summary>
    public class NetworkMessage
    {
        public string Type { get; set; } = string.Empty;        // "command", "event", "auth", "error"
        public string PayloadType { get; set; } = string.Empty; // e.g. "CreateCharacter", "CharacterDamageTaken"
        public string Payload { get; set; } = string.Empty;     // JSON-сериализованные данные
        public string CorrelationId { get; set; } = string.Empty; // для сопоставления запрос-ответ
    }

    /// <summary>
    /// Конфигурация сервера.
    /// </summary>
    public class GameServerConfiguration
    {
        public int WebSocketPort { get; set; } = 5000;
        public int TcpPort { get; set; } = 5001;
        public int MaxConnectionsPerUser { get; set; } = 3;
        public int MaxMessageSizeBytes { get; set; } = 65536;
        public bool RequireAuthentication { get; set; } = true;
        public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Основной сервер игры.
    /// </summary>
    public class GameServer
    {
        private readonly GameServerConfiguration _config;
        private readonly IServiceProvider _serviceProvider;
        private readonly ICommandBus _commandBus;
        private readonly IEventBus _eventBus;
        private readonly ISessionManager _sessionManager;
        private readonly PermissionChecker _permissionChecker;
        private readonly IMetricsCollector _metricsCollector;
        private readonly ITracer _tracer;
        private readonly ILogger<GameServer> _logger;

        // Активные подключения
        private readonly ConcurrentDictionary<Guid, IClientConnection> _connections = new();
        // Сопоставление UserId -> список ConnectionId
        private readonly ConcurrentDictionary<Guid, List<Guid>> _userConnections = new();

        // WebSocket и TCP слушатели
        private HttpListener? _webSocketListener;
        private TcpListener? _tcpListener;

        public GameServer(
            GameServerConfiguration config,
            IServiceProvider serviceProvider,
            ICommandBus commandBus,
            IEventBus eventBus,
            ISessionManager sessionManager,
            PermissionChecker permissionChecker,
            IMetricsCollector metricsCollector,
            ITracer tracer,
            ILogger<GameServer> logger)
        {
            _config = config;
            _serviceProvider = serviceProvider;
            _commandBus = commandBus;
            _eventBus = eventBus;
            _sessionManager = sessionManager;
            _permissionChecker = permissionChecker;
            _metricsCollector = metricsCollector;
            _tracer = tracer;
            _logger = logger;
        }

        /// <summary>
        /// Запустить сервер (WebSocket + TCP).
        /// </summary>
        public async Task Start(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Game Server...");

            // Подписка на события, которые нужно транслировать подключённым клиентам
            _eventBus.Subscribe<IDomainEvent>(OnDomainEvent);

            // Запуск WebSocket слушателя
            _webSocketListener = new HttpListener();
            _webSocketListener.Prefixes.Add($"http://+:{_config.WebSocketPort}/ws/");
            _webSocketListener.Start();
            _ = Task.Run(() => AcceptWebSocketConnections(cancellationToken), cancellationToken);

            // Запуск TCP слушателя
            _tcpListener = new TcpListener(IPAddress.Any, _config.TcpPort);
            _tcpListener.Start();
            _ = Task.Run(() => AcceptTcpConnections(cancellationToken), cancellationToken);

            _logger.LogInformation("Game Server started on ports WS:{WebSocketPort} TCP:{TcpPort}",
                _config.WebSocketPort, _config.TcpPort);

            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        // ---------- WebSocket обработка ----------
        private async Task AcceptWebSocketConnections(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var context = await _webSocketListener!.GetContextAsync();
                if (!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    continue;
                }

                var webSocketContext = await context.AcceptWebSocketAsync(null);
                var connection = new WebSocketClientConnection(webSocketContext.WebSocket, _config.MaxMessageSizeBytes, _logger);
                _connections[connection.ConnectionId] = connection;

                connection.MessageReceived += OnMessageReceived;

                _ = Task.Run(() => ProcessWebSocketConnection(connection, cancellationToken), cancellationToken);
            }
        }

        private async Task ProcessWebSocketConnection(IClientConnection connection, CancellationToken cancellationToken)
        {
            try
            {
                // Цикл чтения сообщений реализован внутри WebSocketClientConnection
                await ((WebSocketClientConnection)connection).ReceiveLoop(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WebSocket connection {ConnectionId} error", connection.ConnectionId);
            }
            finally
            {
                await DisconnectClient(connection);
            }
        }

        // ---------- TCP обработка ----------
        private async Task AcceptTcpConnections(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var tcpClient = await _tcpListener!.AcceptTcpClientAsync(cancellationToken);
                var connection = new TcpClientConnection(tcpClient, _config.MaxMessageSizeBytes, _logger);
                _connections[connection.ConnectionId] = connection;

                connection.MessageReceived += OnMessageReceived;

                _ = Task.Run(() => ProcessTcpConnection(connection, cancellationToken), cancellationToken);
            }
        }

        private async Task ProcessTcpConnection(IClientConnection connection, CancellationToken cancellationToken)
        {
            try
            {
                await ((TcpClientConnection)connection).ReceiveLoop(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TCP connection {ConnectionId} error", connection.ConnectionId);
            }
            finally
            {
                await DisconnectClient(connection);
            }
        }

        // ---------- Обработка входящих сообщений ----------
        private async Task OnMessageReceived(IClientConnection connection, byte[] rawData)
        {
            using var span = _tracer.StartSpan("GameServer.MessageReceived");
            var messageJson = Encoding.UTF8.GetString(rawData);
            NetworkMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<NetworkMessage>(messageJson);
                if (message == null) throw new InvalidOperationException("Empty message.");
            }
            catch (Exception ex)
            {
                await SendError(connection, "Invalid message format", ex.Message);
                return;
            }

            _logger.LogDebug("Received {MessageType} from {ConnectionId}", message.Type, connection.ConnectionId);

            switch (message.Type)
            {
                case "auth":
                    await HandleAuthentication(connection, message);
                    break;
                case "command":
                    await HandleIncomingCommand(connection, message);
                    break;
                case "query":
                    await HandleIncomingQuery(connection, message);
                    break;
                default:
                    await SendError(connection, "Unknown message type", $"Type: {message.Type}");
                    break;
            }
        }

        private async Task HandleAuthentication(IClientConnection connection, NetworkMessage message)
        {
            // Десериализуем payload, ожидаем AuthRequest
            var authRequest = JsonSerializer.Deserialize<AuthRequest>(message.Payload);
            if (authRequest == null || string.IsNullOrEmpty(authRequest.Token))
            {
                await SendError(connection, "Auth failed", "Missing token");
                return;
            }

            // Проверка токена (заглушка)
            // В реальной системе: валидация JWT, получение UserId и разрешений.
            Guid userId = Guid.Parse(authRequest.Token); // предполагаем, что токен = Guid для упрощения
            connection.UserId = userId;
            connection.State = ConnectionState.Authenticated;
            RegisterUserConnection(userId, connection.ConnectionId);

            // Отправить подтверждение
            var response = new NetworkMessage
            {
                Type = "auth_response",
                Payload = JsonSerializer.Serialize(new { Success = true, UserId = userId }),
                CorrelationId = message.CorrelationId
            };
            await SendMessage(connection, response);

            _metricsCollector.IncrementCounter("dnd.connections.authenticated");
        }

        private async Task HandleIncomingCommand(IClientConnection connection, NetworkMessage message)
        {
            if (_config.RequireAuthentication && connection.State != ConnectionState.Authenticated)
            {
                await SendError(connection, "Not authenticated", null);
                return;
            }

            // Десериализуем команду по PayloadType
            var commandType = Type.GetType(message.PayloadType);
            if (commandType == null)
            {
                await SendError(connection, "Unknown command type", message.PayloadType);
                return;
            }

            ICommand? command;
            try
            {
                command = JsonSerializer.Deserialize(message.Payload, commandType) as ICommand;
                if (command == null) throw new InvalidOperationException("Command deserialization failed.");
            }
            catch (Exception ex)
            {
                await SendError(connection, "Invalid command payload", ex.Message);
                return;
            }

            // Создаём контекст выполнения с информацией о пользователе и сессии
            var context = new CommandContext
            {
                UserId = connection.UserId ?? Guid.Empty,
                GameSessionId = connection.SessionId ?? Guid.Empty,
                CancellationToken = CancellationToken.None
            };

            try
            {
                // Отправляем команду в шину
                await _commandBus.SendAsync(command, context);
                _metricsCollector.IncrementCounter("dnd.commands.network_received");
            }
            catch (Exception ex)
            {
                await SendError(connection, "Command execution failed", ex.Message);
            }
        }

        private async Task HandleIncomingQuery(IClientConnection connection, NetworkMessage message)
        {
            // Аналогично HandleIncomingCommand, но для запросов (через IQueryBus)
            // Для упрощения опущено
            await SendError(connection, "Queries not yet supported over network", "");
        }

        // ---------- Рассылка событий клиентам ----------
        private async Task OnDomainEvent(IDomainEvent @event, CancellationToken cancellationToken)
        {
            var message = new NetworkMessage
            {
                Type = "event",
                PayloadType = @event.GetType().AssemblyQualifiedName!,
                Payload = JsonSerializer.Serialize(@event, @event.GetType())
            };

            // Определяем, каким клиентам отправлять. Если событие связано с конкретным персонажем или кампанией,
            // рассылаем только пользователям этой сессии. Для глобальных — всем.
            var affectedSessionIds = GetAffectedSessions(@event);
            var targetConnections = _connections.Values
                .Where(c => c.State == ConnectionState.Authenticated)
                .Where(c => affectedSessionIds == null || (c.SessionId.HasValue && affectedSessionIds.Contains(c.SessionId.Value)));

            foreach (var connection in targetConnections)
            {
                await SendMessage(connection, message);
            }
        }

        private HashSet<Guid>? GetAffectedSessions(IDomainEvent @event)
        {
            // В зависимости от типа события определяем сессии. Упрощённо: все, где есть персонажи.
            // Можно использовать ISessionManager.
            return null; // null = broadcast всем аутентифицированным
        }

        // ---------- Вспомогательные методы ----------
        private async Task SendMessage(IClientConnection connection, NetworkMessage message)
        {
            var json = JsonSerializer.Serialize(message);
            var data = Encoding.UTF8.GetBytes(json);
            try
            {
                await connection.SendAsync(new ArraySegment<byte>(data), CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send message to {ConnectionId}", connection.ConnectionId);
            }
        }

        private async Task SendError(IClientConnection connection, string error, string? detail)
        {
            var message = new NetworkMessage
            {
                Type = "error",
                Payload = JsonSerializer.Serialize(new { Error = error, Detail = detail })
            };
            await SendMessage(connection, message);
        }

        private void RegisterUserConnection(Guid userId, Guid connectionId)
        {
            _userConnections.AddOrUpdate(userId,
                _ => new List<Guid> { connectionId },
                (_, list) => { list.Add(connectionId); return list; });
        }

        private async Task DisconnectClient(IClientConnection connection)
        {
            if (connection.State == ConnectionState.Disconnected) return;
            connection.State = ConnectionState.Disconnecting;
            _connections.TryRemove(connection.ConnectionId, out _);
            if (connection.UserId.HasValue)
            {
                if (_userConnections.TryGetValue(connection.UserId.Value, out var list))
                {
                    list.Remove(connection.ConnectionId);
                    if (list.Count == 0) _userConnections.TryRemove(connection.UserId.Value, out _);
                }
            }
            try { await connection.CloseAsync(CancellationToken.None); } catch { }
            connection.State = ConnectionState.Disconnected;
            _metricsCollector.IncrementCounter("dnd.connections.disconnected");
        }
    }

    // ---------- Вспомогательные классы соединений ----------

    /// <summary>
    /// WebSocket реализация IClientConnection.
    /// </summary>
    public class WebSocketClientConnection : IClientConnection
    {
        private readonly WebSocket _webSocket;
        private readonly int _maxMessageSize;
        private readonly ILogger _logger;

        public Guid ConnectionId { get; } = Guid.NewGuid();
        public Guid? UserId { get; set; }
        public Guid? SessionId { get; set; }
        public ConnectionState State { get; set; } = ConnectionState.Connecting;
        public TransportProtocol Protocol => TransportProtocol.WebSocket;

        public event Func<IClientConnection, byte[], Task>? MessageReceived;

        public WebSocketClientConnection(WebSocket webSocket, int maxMessageSize, ILogger logger)
        {
            _webSocket = webSocket;
            _maxMessageSize = maxMessageSize;
            _logger = logger;
        }

        public async Task SendAsync(ArraySegment<byte> data, CancellationToken cancellationToken)
        {
            if (_webSocket.State == WebSocketState.Open)
                await _webSocket.SendAsync(data, WebSocketMessageType.Text, true, cancellationToken);
        }

        public async Task CloseAsync(CancellationToken cancellationToken)
        {
            if (_webSocket.State == WebSocketState.Open)
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closing", cancellationToken);
            _webSocket.Dispose();
        }

        public async Task ReceiveLoop(CancellationToken cancellationToken)
        {
            var buffer = new byte[_maxMessageSize];
            while (_webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", cancellationToken);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    // Упрощённо: предполагаем, что сообщение помещается в один фрейм. Для реального кода нужна сборка фреймов.
                    var data = new byte[result.Count];
                    Array.Copy(buffer, data, result.Count);
                    if (MessageReceived != null)
                        await MessageReceived.Invoke(this, data);
                }
            }
        }
    }

    /// <summary>
    /// TCP реализация IClientConnection (упрощённая, с разделителем сообщений по длине).
    /// </summary>
    public class TcpClientConnection : IClientConnection
    {
        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _stream;
        private readonly int _maxMessageSize;
        private readonly ILogger _logger;

        public Guid ConnectionId { get; } = Guid.NewGuid();
        public Guid? UserId { get; set; }
        public Guid? SessionId { get; set; }
        public ConnectionState State { get; set; } = ConnectionState.Connecting;
        public TransportProtocol Protocol => TransportProtocol.Tcp;

        public event Func<IClientConnection, byte[], Task>? MessageReceived;

        public TcpClientConnection(TcpClient tcpClient, int maxMessageSize, ILogger logger)
        {
            _tcpClient = tcpClient;
            _stream = tcpClient.GetStream();
            _maxMessageSize = maxMessageSize;
            _logger = logger;
        }

        public async Task SendAsync(ArraySegment<byte> data, CancellationToken cancellationToken)
        {
            // Передаём длину сообщения (4 байта) + данные
            var lengthBytes = BitConverter.GetBytes(data.Count);
            await _stream.WriteAsync(lengthBytes, cancellationToken);
            await _stream.WriteAsync(data, cancellationToken);
        }

        public async Task CloseAsync(CancellationToken cancellationToken)
        {
            _stream.Close();
            _tcpClient.Close();
            await Task.CompletedTask;
        }

        public async Task ReceiveLoop(CancellationToken cancellationToken)
        {
            var lengthBuffer = new byte[4];
            while (!cancellationToken.IsCancellationRequested)
            {
                int read = await _stream.ReadAsync(lengthBuffer, 0, 4, cancellationToken);
                if (read < 4) break;
                int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                if (messageLength <= 0 || messageLength > _maxMessageSize) break;

                var dataBuffer = new byte[messageLength];
                int bytesRead = 0;
                while (bytesRead < messageLength)
                {
                    int r = await _stream.ReadAsync(dataBuffer, bytesRead, messageLength - bytesRead, cancellationToken);
                    if (r == 0) break;
                    bytesRead += r;
                }
                if (bytesRead < messageLength) break;

                if (MessageReceived != null)
                    await MessageReceived.Invoke(this, dataBuffer);
            }
        }
    }

    public class AuthRequest
    {
        public string Token { get; set; } = string.Empty;
    }
}