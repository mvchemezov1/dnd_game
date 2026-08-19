// application/event_handlers/trigger_handler.cs
using dnd_game.Domain.Events;
using dnd_game.Domain.Commands;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using dnd_game.infrastructure.message_bus;
using dnd_game.application.event_handlers;

namespace dnd_game.Application.EventHandlers
{
    /// <summary>
    /// Описывает одно действие внутри скрипта.
    /// </summary>
    public class ScriptAction
    {
        public string ActionType { get; set; } = string.Empty; // "SpawnMonster", "GiveItem", "Teleport", "SetQuestFlag", etc.
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Условие, которое проверяется перед запуском скрипта.
    /// </summary>
    public class TriggerCondition
    {
        public string ConditionType { get; set; } = string.Empty; // "SkillCheck", "HasItem", "IsAlive", "LevelGreaterThan", etc.
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Хранилище определений триггеров, загружаемых из базы данных Мастера.
    /// </summary>
    public interface ITriggerDefinitionRepository
    {
        IEnumerable<TriggerDefinition> GetByEvent(string eventName);
    }

    public class TriggerDefinition
    {
        public Guid TriggerId { get; set; }
        public string EventName { get; set; } = string.Empty;   // имя доменного события, на которое реагируем
        public List<TriggerCondition> Conditions { get; set; } = new();
        public List<ScriptAction> Actions { get; set; } = new();
        public bool IsOneShot { get; set; } = true;             // сработать только один раз
        public int CooldownSeconds { get; set; } = 0;           // перезарядка в секундах (0 – без перезарядки)
        public int DelaySeconds { get; set; } = 0;              // задержка перед выполнением действий
        public int Priority { get; set; } = 0;
    }

    /// <summary>
    /// Состояние конкретного триггера (активен, на перезарядке, уже использован).
    /// </summary>
    public class TriggerState
    {
        public bool HasBeenTriggered { get; set; }
        public DateTime? LastTriggeredUtc { get; set; }
        public DateTime? CooldownEndsUtc { get; set; }
    }

    /// <summary>
    /// Интерфейс для проверки условий (использует read-модель).
    /// </summary>
    public interface IConditionEvaluator
    {
        Task<bool> EvaluateAsync(TriggerCondition condition, IDomainEvent triggeringEvent);
    }

    public class TriggerHandler : IEventHandler<IDomainEvent>, IDisposable
    {
        private readonly ITriggerDefinitionRepository _definitionRepo;
        private readonly IConditionEvaluator _conditionEvaluator;
        private readonly ICommandBus _commandBus;
        private readonly ILogger<TriggerHandler> _logger;

        // Храним состояние триггеров в памяти (в реальной системе – персистентное хранилище)
        private readonly ConcurrentDictionary<Guid, TriggerState> _triggerStates = new();

        public TriggerHandler(
            ITriggerDefinitionRepository definitionRepo,
            IConditionEvaluator conditionEvaluator,
            ICommandBus commandBus,
            ILogger<TriggerHandler> logger)
        {
            _definitionRepo = definitionRepo;
            _conditionEvaluator = conditionEvaluator;
            _commandBus = commandBus;
            _logger = logger;
        }

        public async Task Handle(IDomainEvent @event, CancellationToken cancellationToken)
        {
            // Определяем имя события (можно брать полное имя класса)
            string eventName = @event.GetType().Name;

            // Получаем все определения триггеров, которые реагируют на данный тип события
            var definitions = _definitionRepo.GetByEvent(eventName);
            if (definitions == null || !definitions.Any())
                return;

            foreach (var definition in definitions.OrderBy(d => d.Priority))
            {
                // Проверяем состояние триггера
                var state = _triggerStates.GetOrAdd(definition.TriggerId, _ => new TriggerState());

                // Если одноразовый и уже срабатывал – пропускаем
                if (definition.IsOneShot && state.HasBeenTriggered)
                    continue;

                // Если на перезарядке – пропускаем
                if (state.CooldownEndsUtc.HasValue && state.CooldownEndsUtc.Value > DateTime.UtcNow)
                    continue;

                // Проверяем все условия
                bool conditionsMet = true;
                foreach (var condition in definition.Conditions)
                {
                    if (!await _conditionEvaluator.EvaluateAsync(condition, @event))
                    {
                        conditionsMet = false;
                        break;
                    }
                }
                if (!conditionsMet)
                    continue;

                // Условия выполнены: помечаем триггер как использованный и начинаем перезарядку
                state.HasBeenTriggered = true;
                state.LastTriggeredUtc = DateTime.UtcNow;
                if (definition.CooldownSeconds > 0)
                {
                    state.CooldownEndsUtc = DateTime.UtcNow.AddSeconds(definition.CooldownSeconds);
                }

                _logger.LogInformation("Trigger {TriggerId} activated by event {EventName}", definition.TriggerId, eventName);

                // Применяем задержку
                if (definition.DelaySeconds > 0)
                {
                    _ = ExecuteAfterDelayAsync(definition, cancellationToken);
                }
                else
                {
                    await ExecuteActionsAsync(definition, cancellationToken);
                }
            }
        }

        private async Task ExecuteAfterDelayAsync(TriggerDefinition definition, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(definition.DelaySeconds), cancellationToken);
                await ExecuteActionsAsync(definition, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Delayed trigger {TriggerId} cancelled", definition.TriggerId);
            }
        }

        private async Task ExecuteActionsAsync(TriggerDefinition definition, CancellationToken cancellationToken)
        {
            foreach (var action in definition.Actions)
            {
                try
                {
                    var command = BuildCommand(action);
                    if (command != null)
                    {
                        await _commandBus.SendAsync(command);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to execute trigger action {ActionType} for trigger {TriggerId}",
                        action.ActionType, definition.TriggerId);
                }
            }
        }

        // Фабрика команд по типу действия. Должна быть расширена под конкретную реализацию.
        private ICommand? BuildCommand(ScriptAction action)
        {
            switch (action.ActionType)
            {
                case "SpawnMonster":
                    // Предполагается, что есть команда SpawnMonsterCommand
                     return new SpawnMonsterCommand(
                        (string)action.Parameters["TemplateId"],
                        Convert.ToInt32(action.Parameters["X"]),
                        Convert.ToInt32(action.Parameters["Y"])
                      );
                case "GiveItem":
                     return new GiveItemCommand(
                            (Guid)action.Parameters["CharacterId"],
                            (string)action.Parameters["ItemId"],
                            (string)action.Parameters.GetValueOrDefault("ItemName", (string)action.Parameters["ItemId"]),
                            action.Parameters.ContainsKey("Quantity") ? Convert.ToInt32(action.Parameters["Quantity"]) : 1
                        );
                case "Teleport":
                    return new TeleportCommand(
                        (Guid)action.Parameters["CharacterId"],
                        Convert.ToInt32(action.Parameters["DestinationX"]),
                        Convert.ToInt32(action.Parameters["DestinationY"])
                    );

                case "SetQuestFlag":
                    return new SetQuestFlagCommand(
                        (Guid)action.Parameters["CharacterId"],
                        (string)action.Parameters["QuestId"],
                        (string)action.Parameters["Flag"],
                        (string)action.Parameters["Value"]
                    );

                case "StartDialog":
                    return new StartDialogCommand(
                        (Guid)action.Parameters["InitiatorId"],
                        (string)action.Parameters["DialogId"]
                    );

                case "PlaySound":
                    return new PlaySoundCommand(
                        (string)action.Parameters["SoundName"],
                        Convert.ToInt32(action.Parameters["PositionX"]),
                        Convert.ToInt32(action.Parameters["PositionY"])
                    );

                default:
                    _logger.LogWarning("Unknown trigger action type: {ActionType}", action.ActionType);
                    return null;
            }
        }

        // Сброс состояния триггера (может использоваться для отладки или через консоль Мастера)
        public void ResetTrigger(Guid triggerId)
        {
            _triggerStates.TryRemove(triggerId, out _);
        }

        public void Dispose()
        {
            // очистка ресурсов, если необходимо
        }
    }
}