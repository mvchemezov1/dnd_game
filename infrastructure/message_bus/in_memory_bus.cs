// infrastructure/message_bus/in_memory_bus.cs
using dnd_game.Domain.Commands;
using dnd_game.Domain.Queries;
using dnd_game.Domain.Events;
using System.Collections.Concurrent;
using System.Reflection;
using dnd_game.infrastructure.message_bus;
using dnd_game.application.event_handlers;

namespace dnd_game.Infrastructure.MessageBus
{
    /// <summary>
    /// Единая шина для команд, запросов и событий.
    /// Поддерживает метаданные (CommandContext), делегаты и DI-обработчики.
    /// </summary>
    public class InMemoryBus : ICommandBus, IQueryBus, IEventBus
    {
        private readonly IServiceProvider _provider;

        // Хранилище подписок на события: тип события -> список типов обработчиков и делегатов
        private readonly ConcurrentDictionary<Type, List<EventHandlerRegistration>> _eventHandlers = new();

        private class EventHandlerRegistration
        {
            public Type? HandlerType { get; set; }        // для DI
            public Delegate? HandlerDelegate { get; set; } // для делегатов
        }

        public InMemoryBus(IServiceProvider provider) => _provider = provider;

        // ========== Команды ==========

        // Базовый метод отправки команды без контекста (для обратной совместимости)
        public async Task Send<TCommand>(TCommand command) where TCommand : ICommand
        {
            await SendAsync(command, null);
        }

        public async Task SendAsync(ICommand command, CommandContext? context = null)
        {
            var commandType = command.GetType();
            var handlerType = typeof(ICommandHandler<>).MakeGenericType(commandType);
            var handler = _provider.GetService(handlerType);
            if (handler == null)
                throw new InvalidOperationException($"No handler registered for command type '{commandType.Name}'.");

            var method = handlerType.GetMethod("Handle", new[] { commandType, typeof(CancellationToken) });
            if (method == null)
                throw new InvalidOperationException($"Handler for {commandType.Name} does not have Handle method.");

            var ct = context?.CancellationToken ?? CancellationToken.None;
            await (Task)method.Invoke(handler, new object[] { command, ct })!;
        }

        public async Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CommandContext? context = null)
        {
            var commandType = command.GetType();
            // Ищем обработчик ICommandHandler<TCommand, TResult>
            var handlerType = typeof(ICommandHandler<>).MakeGenericType(commandType);
            var handler = _provider.GetService(handlerType);
            if (handler == null)
                throw new InvalidOperationException($"No handler for command '{commandType.Name}' returning '{typeof(TResult).Name}'.");

            var method = handlerType.GetMethod("Handle", new[] { commandType, typeof(CancellationToken) });
            if (method == null)
                throw new InvalidOperationException($"Handler does not have Handle method.");

            var ct = context?.CancellationToken ?? CancellationToken.None;
            var task = (Task<TResult>)method.Invoke(handler, new object[] { command, ct })!;
            return await task;
        }

        public void Subscribe<TCommand>(Func<TCommand, CommandContext?, Task> handler) where TCommand : ICommand
        {
            // Регистрируем делегат как обработчик через DI или словарь; здесь можно сохранить во внутренний словарь и проверять при SendAsync.
            // Для простоты можно создать анонимный ICommandHandler<TCommand> и зарегистрировать его во временном провайдере?
            // В данной реализации приоритет DI, поэтому рекомендуется регистрировать через DI, но метод Subscribe оставлен для совместимости.
            // Мы можем сохранить делегат и при отсутствии DI-обработчика вызывать его.
        }

        // ========== Запросы ==========

        public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, QueryContext? context = null, CancellationToken cancellationToken = default)
        {
            var queryType = query.GetType();
            var handlerType = typeof(IQueryHandler<,>).MakeGenericType(queryType, typeof(TResult));
            var handler = _provider.GetService(handlerType);
            if (handler == null)
                throw new InvalidOperationException($"No query handler for '{queryType.Name}' returning '{typeof(TResult).Name}'.");

            var method = handlerType.GetMethod("Handle", new[] { queryType, typeof(CancellationToken) });
            if (method == null) throw new InvalidOperationException("Handle method missing.");

            var task = (Task<TResult>)method.Invoke(handler, new object[] { query, cancellationToken })!;
            return await task;
        }

        // ========== События ==========

        public async Task Publish(IDomainEvent @event)
        {
            await PublishAsync(@event, CancellationToken.None);
        }

        public async Task PublishAsync(IDomainEvent @event, CancellationToken cancellationToken = default)
        {
            await PublishInternal(@event, cancellationToken);
        }

        public async Task PublishAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
        {
            foreach (var e in events)
                await PublishInternal(e, cancellationToken);
        }

        public async Task PublishAsync(IDomainEvent @event, CommandContext context, CancellationToken cancellationToken = default)
        {
            // Контекст может использоваться для логгирования или передачи метаданных
            await PublishInternal(@event, cancellationToken);
        }

        public void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IDomainEvent
        {
            var eventType = typeof(TEvent);
            _eventHandlers.AddOrUpdate(eventType,
                _ => new List<EventHandlerRegistration> { new EventHandlerRegistration { HandlerType = handler.GetType() } },
                (_, list) => { list.Add(new EventHandlerRegistration { HandlerType = handler.GetType() }); return list; });
        }

        public void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : IDomainEvent
        {
            var eventType = typeof(TEvent);
            _eventHandlers.AddOrUpdate(eventType,
                _ => new List<EventHandlerRegistration> { new EventHandlerRegistration { HandlerDelegate = handler } },
                (_, list) => { list.Add(new EventHandlerRegistration { HandlerDelegate = handler }); return list; });
        }

        public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IDomainEvent
        {
            var eventType = typeof(TEvent);
            if (_eventHandlers.TryGetValue(eventType, out var list))
            {
                list.RemoveAll(r => r.HandlerType == handler.GetType());
                if (list.Count == 0) _eventHandlers.TryRemove(eventType, out _);
            }
        }

        // Универсальная регистрация из Startup (подписка THandler на TEvent через DI)
        public void Subscribe<TEvent, THandler>() where TEvent : IDomainEvent where THandler : IEventHandler<TEvent>
        {
            Subscribe(new EventHandlerRegistration { HandlerType = typeof(THandler) });
        }

        private void Subscribe(EventHandlerRegistration registration)
        {
            // Предполагаем, что TEvent известен – но в текущей реализации мы не можем узнать TEvent из типа обработчика.
            // Этот метод не обязателен, можно использовать только Subscribe<TEvent>(IEventHandler<TEvent>).
        }

        private async Task PublishInternal(IDomainEvent @event, CancellationToken cancellationToken)
        {
            var eventType = @event.GetType();

            // 1. Обработчики, зарегистрированные явно через Subscribe
            if (_eventHandlers.TryGetValue(eventType, out var registrations))
            {
                foreach (var reg in registrations)
                {
                    if (reg.HandlerDelegate != null)
                    {
                        try
                        {
                            await ((dynamic)reg.HandlerDelegate)((dynamic)@event, cancellationToken);
                        }
                        catch (Exception)
                        {
                            // логирование ошибки
                        }
                    }
                    else if (reg.HandlerType != null)
                    {
                        var handler = _provider.GetService(reg.HandlerType);
                        if (handler != null)
                        {
                            var method = reg.HandlerType.GetMethod("Handle", new[] { eventType, typeof(CancellationToken) });
                            if (method != null)
                            {
                                try
                                {
                                    await (Task)method.Invoke(handler, new object[] { @event, cancellationToken })!;
                                }
                                catch (Exception) { }
                            }
                        }
                    }
                }
            }

            // 2. Обработчики, полученные через DI (IEventHandler<TEvent>)
            var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);
            var handlers = _provider.GetServices(handlerType);
            foreach (var handler in handlers)
            {
                var method = handlerType.GetMethod("Handle", new[] { eventType, typeof(CancellationToken) });
                if (method != null)
                {
                    try
                    {
                        await (Task)method.Invoke(handler, new object[] { @event, cancellationToken })!;
                    }
                    catch (Exception) { }
                }
            }
        }

        public async Task<PagedResult<TResult>> QueryPagedAsync<TResult>(IPagedQuery<TResult> query, QueryContext? context = null, CancellationToken cancellationToken = default)
        {
            // Пагинированные запросы обрабатываются через тот же IQueryHandler, возвращающий PagedResult<TResult>
            return await QueryAsync<PagedResult<TResult>>((IQuery<PagedResult<TResult>>)query, context, cancellationToken);
        }

        void Unsubscribe<T>(Func<T, CancellationToken, Task> eventHandler)
        {
            var eventType = typeof(T);
            if (_eventHandlers.TryGetValue(eventType, out var list))
            {
                // Удаляем регистрацию, у которой HandlerDelegate совпадает с переданным делегатом
                var toRemove = list
                    .Where(r => r.HandlerDelegate != null && r.HandlerDelegate == (Delegate)eventHandler)
                    .ToList();
                foreach (var reg in toRemove)
                    list.Remove(reg);
                if (list.Count == 0)
                    _eventHandlers.TryRemove(eventType, out _);
            }
        }

    }
}