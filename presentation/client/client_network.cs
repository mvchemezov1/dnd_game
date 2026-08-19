// presentation/client/client_network.cs
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using dnd_game.Domain.Commands;
using dnd_game.Domain.Queries;
using dnd_game.Domain.Events;
using dnd_game.Infrastructure.Network; // MessageHeader, MessageType, INetworkMessage, etc.
using dnd_game.Infrastructure.MessageBus; // CommandContext? (может не понадобиться)
using Microsoft.Extensions.Logging;

namespace dnd_game.Presentation.Client
{
    /// <summary>
    /// Состояние подключения клиента.
    /// </summary>
    public enum ClientConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Authenticating,
        Authenticated,
        Reconnecting,
        Disconnecting
    }

    /// <summary>
    /// Конфигурация клиентского подключения.
    /// </summary>
    public class ClientNetworkConfig
    {
        public string ServerUrl { get; set; } = "ws://localhost:5000/ws";
        public int ReconnectDelayMs { get; set; } = 2000;
        public int MaxReconnectAttempts { get; set; } = 5;
        public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan AuthTimeout { get; set; } = TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// Делегат для получения сообщений от сервера (события, ответы).
    /// </summary>
    public delegate Task MessageReceivedHandler(INetworkMessage message);

    /// <summary>
    /// Интерфейс игрового клиента, абстрагирующий сетевое взаимодействие.
    /// </summary>
    public interface IGameClient
    {
        ClientConnectionState State { get; }
        Task ConnectAsync(string? authToken = null);
        Task DisconnectAsync();
        Task SendCommandAsync(ICommand command, string? correlationId = null);
        Task SendQueryAsync<TResult>(IQuery<TResult> query, string? correlationId = null);
        Task SendMessageAsync(INetworkMessage message);
        void RegisterMessageHandler(MessageReceivedHandler handler);
        void UnregisterMessageHandler(MessageReceivedHandler handler);
    }

    /// <summary>
    /// Клиентская сетевая библиотека на основе WebSocket.
    /// </summary>
    public class ClientNetwork : IGameClient, IDisposable
    {
        private readonly ClientNetworkConfig _config;
        private readonly ILogger<ClientNetwork>? _logger;
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _connectionCts;
        private Task? _receiveTask;

        private readonly ConcurrentBag<MessageReceivedHandler> _handlers = new();
        private string? _authToken;
        private int _reconnectAttempts;

        public ClientConnectionState State { get; private set; } = ClientConnectionState.Disconnected;
        public Guid? UserId { get; private set; }

        public ClientNetwork(ClientNetworkConfig config, ILogger<ClientNetwork>? logger = null)
        {
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Подключиться к серверу и опционально отправить токен для аутентификации.
        /// </summary>
        public async Task ConnectAsync(string? authToken = null)
        {
            if (State == ClientConnectionState.Connected || State == ClientConnectionState.Connecting)
                throw new InvalidOperationException("Already connected or connecting.");

            _authToken = authToken;
            State = ClientConnectionState.Connecting;
            _reconnectAttempts = 0;
            await EstablishConnectionAsync();
        }

        private async Task EstablishConnectionAsync()
        {
            try
            {
                _webSocket = new ClientWebSocket();
                _connectionCts = new CancellationTokenSource();
                await _webSocket.ConnectAsync(new Uri(_config.ServerUrl), _connectionCts.Token);

                State = ClientConnectionState.Connected;
                _logger?.LogInformation("WebSocket connected to {ServerUrl}", _config.ServerUrl);

                // Если есть токен, проходим аутентификацию
                if (!string.IsNullOrEmpty(_authToken))
                {
                    State = ClientConnectionState.Authenticating;
                    var authRequest = new AuthRequestMessage { Token = _authToken };
                    await SendMessageAsync(authRequest);

                    // Ожидаем ответ аутентификации в основном цикле приёма (ReceiveLoop обрабатывает)
                    // В реальном коде можно использовать таск с таймаутом для ожидания AuthResponse.
                    // Здесь для упрощения предполагаем, что ReceiveLoop вызовет обновление состояния.
                    // Но для демонстрации создадим временный TaskCompletionSource.
                    // Однако при упрощённом подходе ReceiveLoop просто обрабатывает все сообщения.
                    // Мы можем внутри обработчика сообщений установить UserId и State после успешного AuthResponse.
                }
                else
                {
                    // Гостевое подключение
                    State = ClientConnectionState.Authenticated;
                }

                // Запуск фонового приёма
                _receiveTask = Task.Run(() => ReceiveLoop(_connectionCts.Token));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to connect to server.");
                State = ClientConnectionState.Disconnected;
                // Попытка переподключения
                await TryReconnectAsync();
            }
        }

        /// <summary>
        /// Основной цикл приёма сообщений.
        /// </summary>
        private async Task ReceiveLoop(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            while (_webSocket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await ReceiveFullMessageAsync(_webSocket, buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger?.LogInformation("Server closed connection.");
                        break;
                    }
                    if (result.Data == null || result.Data.Length == 0) continue;

                    // Декодируем сообщение через протокол
                    var protocol = new JsonNetworkProtocol();
                    var messages = protocol.Decode(result.Data);
                    foreach (var msg in messages)
                    {
                        await DispatchMessageAsync(msg);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (WebSocketException ex)
                {
                    _logger?.LogWarning(ex, "WebSocket error in receive loop.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Unexpected error in receive loop.");
                }
            }

            // Соединение потеряно
            State = ClientConnectionState.Disconnected;
            await TryReconnectAsync();
        }

        private async Task<(WebSocketMessageType MessageType, byte[] Data)> ReceiveFullMessageAsync(ClientWebSocket socket, byte[] buffer, CancellationToken cancellationToken)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                    return (result.MessageType, Array.Empty<byte>());
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);
            return (result.MessageType, ms.ToArray());
        }

        private async Task DispatchMessageAsync(INetworkMessage message)
        {
            switch (message)
            {
                case AuthResponseMessage authResponse:
                    if (authResponse.Success)
                    {
                        UserId = authResponse.UserId;
                        State = ClientConnectionState.Authenticated;
                        _logger?.LogInformation("Authenticated as user {UserId}", UserId);
                    }
                    else
                    {
                        _logger?.LogWarning("Authentication failed: {Error}", authResponse.Error);
                        State = ClientConnectionState.Connected; // остаёмся без аутентификации
                    }
                    break;
                case CommandResponseNetworkMessage cmdResponse:
                    _logger?.LogDebug("Command response: {Success}", cmdResponse.Success);
                    break;
                case EventNetworkMessage eventMsg:
                    // Можно десериализовать событие и вызвать специфичные обработчики
                    break;
            }

            // Вызываем все зарегистрированные обработчики
            foreach (var handler in _handlers)
            {
                try
                {
                    await handler(message);
                }
                catch { /* логгирование ошибки обработчика */ }
            }
        }

        /// <summary>
        /// Попытка переподключения с экспоненциальной задержкой.
        /// </summary>
        private async Task TryReconnectAsync()
        {
            if (_reconnectAttempts >= _config.MaxReconnectAttempts)
            {
                _logger?.LogWarning("Max reconnect attempts reached.");
                return;
            }

            _reconnectAttempts++;
            State = ClientConnectionState.Reconnecting;
            int delay = _config.ReconnectDelayMs * (int)Math.Pow(2, _reconnectAttempts - 1);
            _logger?.LogInformation("Reconnecting in {Delay}ms (attempt {Attempt})", delay, _reconnectAttempts);
            await Task.Delay(delay);
            await EstablishConnectionAsync();
        }

        public async Task DisconnectAsync()
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                State = ClientConnectionState.Disconnecting;
                _connectionCts?.Cancel();
                try
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
                }
                catch { }
                State = ClientConnectionState.Disconnected;
                _logger?.LogInformation("Disconnected from server.");
            }
        }

        public async Task SendCommandAsync(ICommand command, string? correlationId = null)
        {
            var msg = NetworkMessageFactory.FromCommand(command, UserId ?? Guid.Empty, Guid.Empty, correlationId);
            await SendMessageAsync(msg);
        }

        public async Task SendQueryAsync<TResult>(IQuery<TResult> query, string? correlationId = null)
        {
            var msg = new QueryNetworkMessage
            {
                QueryTypeName = query.GetType().AssemblyQualifiedName!,
                QueryJson = JsonSerializer.Serialize(query, query.GetType()),
                CorrelationId = correlationId
            };
            await SendMessageAsync(msg);
        }

        public async Task SendMessageAsync(INetworkMessage message)
        {
            if (_webSocket?.State != WebSocketState.Open)
                throw new InvalidOperationException("Not connected.");
            var protocol = new JsonNetworkProtocol();
            var bytes = protocol.Encode(message);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _connectionCts?.Token ?? CancellationToken.None);
        }

        public void RegisterMessageHandler(MessageReceivedHandler handler) => _handlers.Add(handler);
        public void UnregisterMessageHandler(MessageReceivedHandler handler) => _handlers.TryTake(out _);

        public void Dispose()
        {
            _connectionCts?.Cancel();
            _webSocket?.Dispose();
        }
    }
}