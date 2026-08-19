// presentation/client/macros.cs
using System.Collections.Concurrent;
using System.Text.Json;
using dnd_game.Domain.Commands;
using dnd_game.Domain.ValueObjects;
using dnd_game.Infrastructure.Network; // IGameClient
using Microsoft.Extensions.Logging;

namespace dnd_game.Presentation.Client
{
    /// <summary>
    /// Тип шага макроса.
    /// </summary>
    public enum MacroStepType
    {
        SendCommand,       // отправить доменную команду
        Wait,              // подождать миллисекунды
        RollDice,          // бросок костей и сохранение результата
        Conditional,       // если условие истинно, выполнить вложенные шаги
        Repeat,            // повторять вложенные шаги N раз
        SelectTarget,      // интерактивный выбор цели (приостанавливает макрос)
        LogMessage         // вывод сообщения в лог
    }

    /// <summary>
    /// Один шаг макроса.
    /// </summary>
    public class MacroStep
    {
        public MacroStepType Type { get; set; } = MacroStepType.SendCommand;
        public string CommandTypeName { get; set; } = string.Empty; // для SendCommand
        public Dictionary<string, object> CommandParameters { get; set; } = new();
        public int WaitMilliseconds { get; set; }                   // для Wait
        public string DiceNotation { get; set; } = string.Empty;   // для RollDice, например "2d6+3"
        public string VariableName { get; set; } = string.Empty;   // для сохранения результата броска / выбора цели
        public string ConditionExpression { get; set; } = string.Empty; // для Conditional (простое сравнение переменных)
        public List<MacroStep> Children { get; set; } = new();     // вложенные шаги для Conditional/Repeat
        public int RepeatCount { get; set; } = 1;                  // для Repeat
        public string Message { get; set; } = string.Empty;        // для LogMessage
    }

    /// <summary>
    /// Полное определение макроса.
    /// </summary>
    public class MacroDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<MacroStep> Steps { get; set; } = new();
        public bool IsSystem { get; set; } // встроенный системный макрос
    }

    /// <summary>
    /// Состояние выполнения одного экземпляра макроса.
    /// </summary>
    public class MacroExecutionContext
    {
        public Guid ControlledCharacterId { get; set; }
        public Guid? ActiveCombatId { get; set; }
        public Dictionary<string, object> Variables { get; set; } = new();
        public CancellationToken CancellationToken { get; set; }
    }

    /// <summary>
    /// Репозиторий макросов (загрузка из файлов, БД или предустановленных).
    /// </summary>
    public interface IMacroRepository
    {
        MacroDefinition? GetByName(string name);
        List<MacroDefinition> GetAll();
        void Save(MacroDefinition macro);
        void Delete(string name);
    }

    /// <summary>
    /// Система макросов, позволяющая игрокам автоматизировать последовательности действий.
    /// </summary>
    public class MacroEngine
    {
        private readonly IMacroRepository _repository;
        private readonly IGameClient _client;
        private readonly ILogger<MacroEngine> _logger;
        private readonly ConcurrentDictionary<string, MacroDefinition> _builtinMacros;

        public MacroEngine(IMacroRepository repository, IGameClient client, ILogger<MacroEngine> logger)
        {
            _repository = repository;
            _client = client;
            _logger = logger;
            _builtinMacros = new ConcurrentDictionary<string, MacroDefinition>();
            RegisterBuiltinMacros();
        }

        /// <summary>
        /// Выполнить макрос по имени с заданным контекстом.
        /// </summary>
        public async Task ExecuteMacro(string macroName, MacroExecutionContext context)
        {
            var macro = _repository.GetByName(macroName) ?? _builtinMacros.GetValueOrDefault(macroName);
            if (macro == null)
            {
                _logger.LogWarning("Macro '{MacroName}' not found.", macroName);
                return;
            }
            await ExecuteSteps(macro.Steps, context);
        }

        private async Task ExecuteSteps(List<MacroStep> steps, MacroExecutionContext context)
        {
            foreach (var step in steps)
            {
                if (context.CancellationToken.IsCancellationRequested) break;

                switch (step.Type)
                {
                    case MacroStepType.SendCommand:
                        await ExecuteSendCommand(step, context);
                        break;
                    case MacroStepType.Wait:
                        await Task.Delay(step.WaitMilliseconds, context.CancellationToken);
                        break;
                    case MacroStepType.RollDice:
                        ExecuteRollDice(step, context);
                        break;
                    case MacroStepType.Conditional:
                        if (EvaluateCondition(step.ConditionExpression, context))
                            await ExecuteSteps(step.Children, context);
                        break;
                    case MacroStepType.Repeat:
                        for (int i = 0; i < step.RepeatCount; i++)
                            await ExecuteSteps(step.Children, context);
                        break;
                    case MacroStepType.SelectTarget:
                        // Интерактивный выбор цели: помечаем макрос как ожидающий, ввод обрабатывается отдельно.
                        // Упрощённо: сохраняем идентификатор цели в переменную, если она уже есть в контексте.
                        break;
                    case MacroStepType.LogMessage:
                        _logger.LogInformation("Macro message: {Message}", step.Message);
                        break;
                }
            }
        }

        private async Task ExecuteSendCommand(MacroStep step, MacroExecutionContext context)
        {
            var commandType = Type.GetType(step.CommandTypeName);
            if (commandType == null) return;

            // Подстановка переменных в параметры команды
            var resolvedParams = new Dictionary<string, object>();
            foreach (var kvp in step.CommandParameters)
            {
                object value = kvp.Value;
                if (value is string strValue && strValue.StartsWith('$'))
                {
                    string varName = strValue[1..];
                    if (context.Variables.TryGetValue(varName, out var varValue))
                        value = varValue;
                }
                resolvedParams[kvp.Key] = value;
            }

            // Создаём экземпляр команды через рефлексию или фабрику.
            // Упрощённо: предполагаем, что у команды есть конструктор с словарём или отдельные поля.
            // Для реальной реализации нужен маппинг, здесь демонстрация.
            ICommand? command = null;
            try
            {
                // Пытаемся создать команду, используя公開API команд (record с свойствами).
                // Используем JsonSerializer для заполнения из словаря.
                var json = JsonSerializer.Serialize(resolvedParams);
                command = (ICommand?)JsonSerializer.Deserialize(json, commandType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create command {CommandType} for macro", step.CommandTypeName);
            }

            if (command != null)
                await _client.SendCommandAsync(command);
        }

        private void ExecuteRollDice(MacroStep step, MacroExecutionContext context)
        {
            try
            {
                var dice = Dice.Parse(step.DiceNotation);
                var random = new Random();
                var result = dice.Roll(random);
                context.Variables[step.VariableName] = result.Total;
                context.Variables[$"{step.VariableName}_details"] = result.KeptRolls.ToList();
                _logger.LogDebug("Macro rolled {Notation} = {Total}", step.DiceNotation, result.Total);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invalid dice notation in macro: {Notation}", step.DiceNotation);
            }
        }

        private bool EvaluateCondition(string expression, MacroExecutionContext context)
        {
            // Простейший парсинг: "variable operator value"
            // Поддерживает: "VarName == 10", "VarName >= 5", "VarName != 0"
            var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3) return false;

            string varName = parts[0];
            string op = parts[1];
            string rightStr = parts[2];

            if (!context.Variables.TryGetValue(varName, out var leftValue)) return false;

            // Приводим к double для сравнения
            double left = Convert.ToDouble(leftValue);
            double right = Convert.ToDouble(rightStr);

            return op switch
            {
                "==" => left == right,
                "!=" => left != right,
                ">" => left > right,
                "<" => left < right,
                ">=" => left >= right,
                "<=" => left <= right,
                _ => false
            };
        }

        // ---------- Встроенные макросы D&D ----------
        private void RegisterBuiltinMacros()
        {
            // Макрос "FullAttack" – несколько атак с основным действием и бонусным
            _builtinMacros["fullattack"] = new MacroDefinition
            {
                Name = "fullattack",
                Description = "Performs all available attacks (main action + bonus action) on the current target.",
                IsSystem = true,
                Steps = new List<MacroStep>
                {
                    new MacroStep { Type = MacroStepType.SendCommand, CommandTypeName = "dnd_game.Domain.Commands.TakeStandardAction",
                        CommandParameters = new Dictionary<string, object> { { "ParticipantId", "$characterId" }, { "ActionType", "Attack" }, { "TargetId", "$targetId" } } },
                    new MacroStep { Type = MacroStepType.Conditional, ConditionExpression = "hasBonusAction == 1",
                        Children = new List<MacroStep> {
                            new MacroStep { Type = MacroStepType.SendCommand, CommandTypeName = "dnd_game.Domain.Commands.TakeBonusAction",
                                CommandParameters = new Dictionary<string, object> { { "ParticipantId", "$characterId" }, { "ActionType", "OffhandAttack" }, { "TargetId", "$targetId" } } }
                        }
                    }
                }
            };

            // Макрос "SecondWind" – боец использует Второе дыхание
            _builtinMacros["secondwind"] = new MacroDefinition
            {
                Name = "secondwind",
                Description = "Use Second Wind: regain 1d10 + fighter level HP.",
                IsSystem = true,
                Steps = new List<MacroStep>
                {
                    new MacroStep { Type = MacroStepType.RollDice, DiceNotation = "1d10", VariableName = "secondwind_roll" },
                    new MacroStep { Type = MacroStepType.SendCommand, CommandTypeName = "dnd_game.Domain.Commands.HealCharacter",
                        CommandParameters = new Dictionary<string, object> { { "CharacterId", "$characterId" }, { "Amount", "$secondwind_roll + $level" } } }
                }
            };

            // Макрос "CastFireball" – бросок 8d6 и применение урона к целям
            _builtinMacros["fireball"] = new MacroDefinition
            {
                Name = "fireball",
                Description = "Cast Fireball: 8d6 fire damage, DC save based on caster.",
                IsSystem = true,
                Steps = new List<MacroStep>
                {
                    new MacroStep { Type = MacroStepType.RollDice, DiceNotation = "8d6", VariableName = "fireball_damage" },
                    new MacroStep { Type = MacroStepType.LogMessage, Message = "Fireball explodes!" },
                    // Здесь должна быть отправка команды на применение урона к нескольким целям (AreaOfEffectSpell)
                }
            };
        }
    }
}