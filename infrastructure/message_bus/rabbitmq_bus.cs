// infrastructure/message_bus/rabbitmq_bus.cs
using dnd_game.Domain.Commands;
using dnd_game.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using dnd_game.infrastructure.message_bus;
using dnd_game.application.event_handlers;

namespace dnd_game.Infrastructure.MessageBus
{
    public class RabbitMqBus : ICommandBus, IEventBus, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IChannel _channel;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RabbitMqBus> _logger;

        private const string CommandExchange = "dnd.commands";
        private const string EventExchange = "dnd.events";
        private const string DeadLetterExchange = "dnd.dead_letter";

        private readonly ConcurrentDictionary<Type, List<Func<ICommand, CommandContext?, Task>>> _commandHandlers = new();
        private readonly ConcurrentDictionary<Type, List<object>> _eventHandlers = new();

        public RabbitMqBus(string connectionString, IServiceProvider serviceProvider, ILogger<RabbitMqBus> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
            _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

            InitializeExchanges();
            StartConsuming();
        }

        private void InitializeExchanges()
        {
            _channel.ExchangeDeclareAsync(CommandExchange, ExchangeType.Direct, durable: true).GetAwaiter().GetResult();
            _channel.ExchangeDeclareAsync(EventExchange, ExchangeType.Topic, durable: true).GetAwaiter().GetResult();
            _channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Direct, durable: true).GetAwaiter().GetResult();

            var deadLetterQueue = "dnd.dead_letter_queue";
            _channel.QueueDeclareAsync(deadLetterQueue, durable: true, exclusive: false, autoDelete: false).GetAwaiter().GetResult();
            _channel.QueueBindAsync(deadLetterQueue, DeadLetterExchange, "#").GetAwaiter().GetResult();
        }

        private void StartConsuming()
        {
            var commandQueue = "dnd.commands.queue";
            _channel.QueueDeclareAsync(commandQueue, durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?> { { "x-dead-letter-exchange", DeadLetterExchange }, { "x-dead-letter-routing-key", "command" } })
                .GetAwaiter().GetResult();
            _channel.QueueBindAsync(commandQueue, CommandExchange, "#").GetAwaiter().GetResult();

            var commandConsumer = new AsyncEventingBasicConsumer(_channel);
            commandConsumer.ReceivedAsync += async (sender, args) =>
            {
                await ProcessCommandMessage(args);
            };
            _channel.BasicConsumeAsync(commandQueue, autoAck: false, commandConsumer).GetAwaiter().GetResult();
        }

        async Task ICommandBus.SendAsync(ICommand command, CommandContext? context)
        {
            context ??= new CommandContext();
            var message = SerializeCommand(command, context);
            var routingKey = command.GetType().Name;

            await _channel.BasicPublishAsync(
                exchange: CommandExchange,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: CreateBasicProperties(command, context),
                body: message);
        }

        void ICommandBus.Subscribe<TCommand>(Func<TCommand, CommandContext?, Task> handler)
        {
            var commandType = typeof(TCommand);
            _commandHandlers.AddOrUpdate(
                commandType,
                _ => new List<Func<ICommand, CommandContext?, Task>> { (cmd, ctx) => handler((TCommand)cmd, ctx) },
                (_, list) => { list.Add((cmd, ctx) => handler((TCommand)cmd, ctx)); return list; });
        }

        private async Task ProcessCommandMessage(BasicDeliverEventArgs args)
        {
            var json = Encoding.UTF8.GetString(args.Body.Span);
            CommandEnvelope? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<CommandEnvelope>(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize command message");
                await _channel.BasicNackAsync(args.DeliveryTag, false, false);
                return;
            }

            if (envelope == null || string.IsNullOrEmpty(envelope.CommandType))
            {
                await _channel.BasicNackAsync(args.DeliveryTag, false, false);
                return;
            }

            Type? commandType = Type.GetType(envelope.CommandType);
            if (commandType == null)
            {
                _logger.LogWarning("Unknown command type: {CommandType}", envelope.CommandType);
                await _channel.BasicNackAsync(args.DeliveryTag, false, false);
                return;
            }

            var commandObj = JsonSerializer.Deserialize(envelope.CommandData, commandType);
            if (commandObj is not ICommand command)
            {
                await _channel.BasicNackAsync(args.DeliveryTag, false, false);
                return;
            }

            var context = new CommandContext
            {
                UserId = envelope.UserId,
                GameSessionId = envelope.SessionId,
                CancellationToken = CancellationToken.None
            };

            try
            {
                var handlerType = typeof(ICommandHandler<>).MakeGenericType(commandType);
                var handler = _serviceProvider.GetService(handlerType);
                if (handler != null)
                {
                    var method = handlerType.GetMethod("Handle", new[] { commandType, typeof(CancellationToken) });
                    if (method != null)
                    {
                        await (Task)method.Invoke(handler, new object[] { command, context.CancellationToken })!;
                        await _channel.BasicAckAsync(args.DeliveryTag, false);
                        return;
                    }
                }

                if (_commandHandlers.TryGetValue(commandType, out var handlers))
                {
                    foreach (var h in handlers) await h(command, context);
                    await _channel.BasicAckAsync(args.DeliveryTag, false);
                    return;
                }

                _logger.LogWarning("No handler for command {CommandType}", commandType.Name);
                await _channel.BasicNackAsync(args.DeliveryTag, false, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling command {CommandType}", commandType.Name);
                await _channel.BasicNackAsync(args.DeliveryTag, false, false);
            }
        }

        async Task IEventBus.PublishAsync(IDomainEvent @event, CancellationToken cancellationToken)
        {
            var eventType = @event.GetType();
            var envelope = new EventEnvelope
            {
                EventType = eventType.AssemblyQualifiedName!,
                EventData = JsonSerializer.Serialize(@event, eventType),
                Timestamp = DateTime.UtcNow
            };
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope));
            var routingKey = eventType.Name;

            await _channel.BasicPublishAsync(
                exchange: EventExchange,
                routingKey: routingKey,
                body: body);
        }

        async Task IEventBus.PublishAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken)
        {
            foreach (var e in events) await ((IEventBus)this).PublishAsync(e, cancellationToken);
        }

        async Task IEventBus.PublishAsync(IDomainEvent @event, CommandContext context, CancellationToken cancellationToken)
        {
            await ((IEventBus)this).PublishAsync(@event, cancellationToken);
        }

        void IEventBus.Subscribe<TEvent>(IEventHandler<TEvent> handler)
        {
            var eventType = typeof(TEvent);
            var queueName = $"dnd.event.{eventType.Name}.{Guid.NewGuid()}";
            _channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: true,
                arguments: new Dictionary<string, object?> { { "x-dead-letter-exchange", DeadLetterExchange }, { "x-dead-letter-routing-key", $"event.{eventType.Name}" } })
                .GetAwaiter().GetResult();
            _channel.QueueBindAsync(queueName, EventExchange, eventType.Name).GetAwaiter().GetResult();

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (sender, args) =>
            {
                var json = Encoding.UTF8.GetString(args.Body.Span);
                try
                {
                    var envelope = JsonSerializer.Deserialize<EventEnvelope>(json);
                    if (envelope != null && envelope.EventType != null)
                    {
                        var type = Type.GetType(envelope.EventType);
                        if (type != null)
                        {
                            var eventObj = JsonSerializer.Deserialize(envelope.EventData, type) as IDomainEvent;
                            if (eventObj != null)
                            {
                                await handler.Handle((TEvent)eventObj, CancellationToken.None);
                                await _channel.BasicAckAsync(args.DeliveryTag, false);
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, "Error processing event {EventType}", eventType.Name); }
                await _channel.BasicNackAsync(args.DeliveryTag, false, false);
            };
            _channel.BasicConsumeAsync(queueName, autoAck: false, consumer).GetAwaiter().GetResult();

            _eventHandlers.AddOrUpdate(eventType,
                _ => new List<object> { handler },
                (_, list) => { list.Add(handler); return list; });
        }

        void IEventBus.Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        {
            ((IEventBus)this).Subscribe(new DelegateEventHandler<TEvent>(handler));
        }

        void IEventBus.Unsubscribe<TEvent>(IEventHandler<TEvent> handler)
        {
            if (_eventHandlers.TryGetValue(typeof(TEvent), out var list)) list.Remove(handler);
        }

        private BasicProperties CreateBasicProperties(ICommand command, CommandContext context)
        {
            var props = new BasicProperties
            {
                Persistent = true,
                Headers = new Dictionary<string, object?>
                {
                    {"UserId", context.UserId.ToString()},
                    {"SessionId", context.GameSessionId.ToString()}
                }
            };
            if (command is IIdempotentCommand idemp)
                props.MessageId = idemp.IdempotencyKey.ToString();
            return props;
        }

        private byte[] SerializeCommand(ICommand command, CommandContext context)
        {
            var envelope = new CommandEnvelope
            {
                CommandType = command.GetType().AssemblyQualifiedName!,
                CommandData = JsonSerializer.Serialize(command, command.GetType()),
                UserId = context.UserId,
                SessionId = context.GameSessionId,
                Timestamp = DateTime.UtcNow
            };
            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope));
        }

        void IDisposable.Dispose()
        {
            _channel?.CloseAsync().GetAwaiter().GetResult();
            _connection?.CloseAsync().GetAwaiter().GetResult();
        }

        private class CommandEnvelope
        {
            public string CommandType { get; set; } = string.Empty;
            public string CommandData { get; set; } = string.Empty;
            public Guid UserId { get; set; }
            public Guid SessionId { get; set; }
            public DateTime Timestamp { get; set; }
        }

        private class EventEnvelope
        {
            public string EventType { get; set; } = string.Empty;
            public string EventData { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
        }

        private class DelegateEventHandler<TEvent> : IEventHandler<TEvent> where TEvent : IDomainEvent
        {
            private readonly Func<TEvent, CancellationToken, Task> _handler;
            public DelegateEventHandler(Func<TEvent, CancellationToken, Task> handler) => _handler = handler;
            public Task Handle(TEvent @event, CancellationToken cancellationToken) => _handler(@event, cancellationToken);
            public Func<TEvent, CancellationToken, Task> Handler => _handler;
        }
    }
}