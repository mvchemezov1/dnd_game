// infrastructure/monitoring/tracer.cs
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace dnd_game.Infrastructure.Monitoring
{
    /// <summary>
    /// Интерфейс распределённой трассировки для DnD приложения.
    /// </summary>
    public interface ITracer
    {
        IDisposable StartSpan(string name);
        IDisposable StartSpan(string name, SpanContext parentContext);
        System.Diagnostics.Activity? CurrentSpan { get; }   // было TelemetrySpan?
        void SetTag(string key, string? value);
        void AddEvent(string eventName);
        void RecordException(Exception ex);
    }

    /// <summary>
    /// Реализация трейсера на основе OpenTelemetry.
    /// Интегрируется с ActivitySource и позволяет экспортировать трассы в Jaeger, Zipkin и т.д.
    /// </summary>
    public class OpenTelemetryTracer : ITracer
    {
        private readonly ActivitySource _activitySource;
        private readonly ILogger<OpenTelemetryTracer> _logger;

        public OpenTelemetryTracer(ActivitySource activitySource, ILogger<OpenTelemetryTracer> logger)
        {
            _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
            _logger = logger;
        }

        public System.Diagnostics.Activity? CurrentSpan => Activity.Current;

        public IDisposable StartSpan(string name)
        {
            var activity = _activitySource.StartActivity(name, ActivityKind.Internal);
            if (activity == null)
            {
                _logger.LogTrace("Span '{SpanName}' not created (no listeners).", name);
                return NoopSpan.Instance;
            }
            return new OpenTelemetrySpan(activity);
        }

        public IDisposable StartSpan(string name, SpanContext parentContext)
        {
            var activity = _activitySource.StartActivity(name, ActivityKind.Internal, parentContext);
            if (activity == null)
                return NoopSpan.Instance;
            return new OpenTelemetrySpan(activity);
        }

        public void SetTag(string key, string? value) => Activity.Current?.SetTag(key, value);
        public void AddEvent(string eventName) => Activity.Current?.AddEvent(new ActivityEvent(eventName));

        public void RecordException(Exception ex)
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
            Activity.Current?.AddException(ex);   // заменено RecordException -> AddException
        }

        private class OpenTelemetrySpan : IDisposable
        {
            private readonly Activity _activity;
            public OpenTelemetrySpan(Activity activity) => _activity = activity;
            public void Dispose() => _activity.Dispose();
        }

        private class NoopSpan : IDisposable
        {
            public static readonly NoopSpan Instance = new();
            public void Dispose() { }
        }
    }

    /// <summary>
    /// Упрощённый трейсер без внешней зависимости (использует System.Diagnostics.Activity).
    /// </summary>
    public class SimpleTracer : ITracer
    {
        private readonly ActivitySource _activitySource;
        private readonly ILogger<SimpleTracer> _logger;

        public SimpleTracer(ILogger<SimpleTracer> logger)
        {
            _activitySource = new ActivitySource("DnD.Game");
            _logger = logger;
        }

        public System.Diagnostics.Activity? CurrentSpan => Activity.Current;

        // OpenTelemetryTracer (строка 60)
        public void RecordException(Exception ex)
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
            Activity.Current?.AddException(ex);
        }


        public IDisposable StartSpan(string name)
        {
            var activity = _activitySource.StartActivity(name, ActivityKind.Internal);
            if (activity == null) return NoopDisposable.Instance;
            _logger.LogTrace("Trace span started: {SpanName} ({SpanId})", activity.OperationName, activity.SpanId);
            return activity;
        }

        public IDisposable StartSpan(string name, SpanContext parentContext)
        {
            var activity = _activitySource.StartActivity(name, ActivityKind.Internal, parentContext);
            if (activity == null) return NoopDisposable.Instance;
            _logger.LogTrace("Trace span (child) started: {SpanName} ({SpanId})", activity.OperationName, activity.SpanId);
            return activity;
        }

        public void SetTag(string key, string? value) => Activity.Current?.SetTag(key, value);
        public void AddEvent(string eventName) => Activity.Current?.AddEvent(new ActivityEvent(eventName));

        private class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new();
            public void Dispose() { }
        }
    }

    /// <summary>
    /// Фабрика трассировки для создания именованных спанов с автоматическим добавлением атрибутов DnD.
    /// </summary>
    public static class TracerExtensions
    {
        public static IDisposable StartCommandSpan(this ITracer tracer, string commandType, Guid userId, Guid sessionId)
        {
            var span = tracer.StartSpan($"Command.{commandType}");
            tracer.SetTag("command.type", commandType);
            tracer.SetTag("user.id", userId.ToString());
            tracer.SetTag("session.id", sessionId.ToString());
            return span;
        }

        public static IDisposable StartQuerySpan(this ITracer tracer, string queryType, Guid userId, Guid sessionId)
        {
            var span = tracer.StartSpan($"Query.{queryType}");
            tracer.SetTag("query.type", queryType);
            tracer.SetTag("user.id", userId.ToString());
            tracer.SetTag("session.id", sessionId.ToString());
            return span;
        }

        public static IDisposable StartEventSpan(this ITracer tracer, string eventType)
        {
            var span = tracer.StartSpan($"Event.{eventType}");
            tracer.SetTag("event.type", eventType);
            return span;
        }

        public static IDisposable StartCombatSpan(this ITracer tracer, Guid combatId, int round)
        {
            var span = tracer.StartSpan($"Combat.Round{round}");
            tracer.SetTag("combat.id", combatId.ToString());
            tracer.SetTag("combat.round", round.ToString());
            return span;
        }

        public static IDisposable StartCharacterActionSpan(this ITracer tracer, Guid characterId, string action)
        {
            var span = tracer.StartSpan($"Character.{action}");
            tracer.SetTag("character.id", characterId.ToString());
            tracer.SetTag("action", action);
            return span;
        }
    }
}