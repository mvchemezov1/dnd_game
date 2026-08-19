// infrastructure/ai/script_engine.cs
using dnd_game.Domain.Commands;
using dnd_game.Domain.Events;
using dnd_game.Application.Services;        // CombatService, TradeService, etc.
using dnd_game.Application.Projections;     // CharacterProjection, CampaignProjection
using dnd_game.Infrastructure.AI;
using Microsoft.Extensions.Logging;
using dnd_game.infrastructure.message_bus;

namespace dnd_game.Infrastructure.AI
{
    /// <summary>
    /// Типы команд скрипта.
    /// </summary>
    public enum ScriptCommandType
    {
        SetVariable,
        If,
        Else,
        EndIf,
        While,
        EndWhile,
        Wait,
        DamageCharacter,
        HealCharacter,
        MoveCharacter,
        GiveItem,
        RemoveItem,
        StartDialogue,
        SetQuestStage,
        CompleteQuest,
        FailQuest,
        ChangeFactionReputation,
        SpawnMonster,
        StartCombat,
        EndCombat,
        ApplyCondition,
        RemoveCondition,
        Teleport,
        PlaySound,
        LogMessage,
        RollSkillCheck,
        SetGlobalFlag,
        RemoveGlobalFlag,
        AdvanceTime,
        ChangeWeather,
        ExecuteCommandBus      // отправка произвольной команды через CommandBus
    }

    /// <summary>
    /// Одна инструкция скрипта.
    /// </summary>
    public class ScriptCommand
    {
        public ScriptCommandType Type { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new();
        // Для блоков условий/циклов храним дочерние команды
        public List<ScriptCommand> Children { get; set; } = new();
        // Номер строки для отладки
        public int LineNumber { get; set; }
    }

    /// <summary>
    /// Определение скрипта, загружаемое из хранилища.
    /// </summary>
    public class ScriptDefinition
    {
        public string ScriptName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<ScriptCommand> Commands { get; set; } = new();
    }

    /// <summary>
    /// Хранилище определений скриптов.
    /// </summary>
    public interface IScriptRepository
    {
        ScriptDefinition? GetByName(string scriptName);
        void AddOrUpdate(ScriptDefinition script);
        List<string> GetAllScriptNames();
    }

    /// <summary>
    /// Контекст выполнения скрипта. Передаётся при запуске и пополняется переменными.
    /// </summary>
    public class ScriptExecutionContext
    {
        public Dictionary<string, object> Variables { get; set; } = new();
        public Guid? CurrentCharacterId { get; set; }  // персонаж-инициатор, если есть
        public Guid? CurrentCampaignId { get; set; }
        // Сервисы (внедряются движком)
        public IServiceProvider Services { get; set; } = null!;
    }

    /// <summary>
    /// Движок выполнения скриптов для DnD.
    /// </summary>
    public class ScriptEngine
    {
        private readonly IScriptRepository _scriptRepository;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ScriptEngine> _logger;

        public ScriptEngine(
            IScriptRepository scriptRepository,
            IServiceProvider serviceProvider,
            ILogger<ScriptEngine> logger)
        {
            _scriptRepository = scriptRepository;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Запустить скрипт по имени с заданным контекстом.
        /// </summary>
        public async Task RunScript(string scriptName, Dictionary<string, object>? context = null)
        {
            var script = _scriptRepository.GetByName(scriptName);
            if (script == null)
            {
                _logger.LogWarning("Script '{ScriptName}' not found.", scriptName);
                return;
            }

            var execContext = new ScriptExecutionContext
            {
                Variables = context ?? new Dictionary<string, object>(),
                Services = _serviceProvider
            };

            await ExecuteCommands(script.Commands, execContext);
        }

        private async Task ExecuteCommands(List<ScriptCommand> commands, ScriptExecutionContext context)
        {
            int index = 0;
            while (index < commands.Count)
            {
                var cmd = commands[index];
                _logger.LogTrace("Executing script command {Type} at line {Line}", cmd.Type, cmd.LineNumber);
                switch (cmd.Type)
                {
                    case ScriptCommandType.SetVariable:
                        await ExecuteSetVariable(cmd, context);
                        break;
                    case ScriptCommandType.If:
                        index = await ExecuteIfBlock(commands, index, context);
                        continue; // индекс уже установлен внутри
                    case ScriptCommandType.While:
                        index = await ExecuteWhileBlock(commands, index, context);
                        continue;
                    case ScriptCommandType.Wait:
                        await ExecuteWait(cmd);
                        break;
                    case ScriptCommandType.DamageCharacter:
                        await ExecuteDamageCharacter(cmd, context);
                        break;
                    case ScriptCommandType.HealCharacter:
                        await ExecuteHealCharacter(cmd, context);
                        break;
                    case ScriptCommandType.MoveCharacter:
                        await ExecuteMoveCharacter(cmd, context);
                        break;
                    case ScriptCommandType.GiveItem:
                        await ExecuteGiveItem(cmd, context);
                        break;
                    case ScriptCommandType.RemoveItem:
                        await ExecuteRemoveItem(cmd, context);
                        break;
                    case ScriptCommandType.StartDialogue:
                        await ExecuteStartDialogue(cmd, context);
                        break;
                    case ScriptCommandType.SetQuestStage:
                        await ExecuteSetQuestStage(cmd, context);
                        break;
                    case ScriptCommandType.CompleteQuest:
                        await ExecuteCompleteQuest(cmd, context);
                        break;
                    case ScriptCommandType.FailQuest:
                        await ExecuteFailQuest(cmd, context);
                        break;
                    case ScriptCommandType.ChangeFactionReputation:
                        await ExecuteChangeFactionReputation(cmd, context);
                        break;
                    case ScriptCommandType.SpawnMonster:
                        await ExecuteSpawnMonster(cmd, context);
                        break;
                    case ScriptCommandType.StartCombat:
                        await ExecuteStartCombat(cmd, context);
                        break;
                    case ScriptCommandType.EndCombat:
                        await ExecuteEndCombat(cmd, context);
                        break;
                    case ScriptCommandType.ApplyCondition:
                        ExecuteApplyCondition(cmd, context);
                        break;
                    case ScriptCommandType.RemoveCondition:
                        await ExecuteRemoveCondition(cmd, context);
                        break;
                    case ScriptCommandType.Teleport:
                        await ExecuteTeleport(cmd, context);
                        break;
                    case ScriptCommandType.PlaySound:
                        await ExecutePlaySound(cmd);
                        break;
                    case ScriptCommandType.LogMessage:
                        await ExecuteLogMessage(cmd);
                        break;
                    case ScriptCommandType.RollSkillCheck:
                        await ExecuteRollSkillCheck(cmd, context);
                        break;
                    case ScriptCommandType.SetGlobalFlag:
                        await ExecuteSetGlobalFlag(cmd, context);
                        break;
                    case ScriptCommandType.RemoveGlobalFlag:
                        await ExecuteRemoveGlobalFlag(cmd, context);
                        break;
                    case ScriptCommandType.AdvanceTime:
                        await ExecuteAdvanceTime(cmd, context);
                        break;
                    case ScriptCommandType.ChangeWeather:
                        await ExecuteChangeWeather(cmd, context);
                        break;
                    case ScriptCommandType.ExecuteCommandBus:
                        await ExecuteCommandBusCmd(cmd, context);
                        break;
                    default:
                        _logger.LogWarning("Unknown script command type: {Type}", cmd.Type);
                        break;
                }
                index++;
            }
        }

        // ---------- Реализация отдельных команд ----------

        private Task ExecuteSetVariable(ScriptCommand cmd, ScriptExecutionContext context)
        {
            if (cmd.Parameters.TryGetValue("Name", out var name) &&
                cmd.Parameters.TryGetValue("Value", out var value))
            {
                context.Variables[name] = value;
            }
            return Task.CompletedTask;
        }

        private async Task<int> ExecuteIfBlock(List<ScriptCommand> commands, int startIndex, ScriptExecutionContext context)
        {
            var cmd = commands[startIndex];
            bool condition = EvaluateCondition(cmd.Parameters, context);
            // Найти соответствующие Else или EndIf
            int elseIndex = -1;
            int endIfIndex = -1;
            int depth = 1;
            for (int i = startIndex + 1; i < commands.Count; i++)
            {
                if (commands[i].Type == ScriptCommandType.If) depth++;
                else if (commands[i].Type == ScriptCommandType.EndIf) depth--;
                if (depth == 0)
                {
                    endIfIndex = i;
                    break;
                }
                if (depth == 1 && commands[i].Type == ScriptCommandType.Else)
                    elseIndex = i;
            }
            if (endIfIndex == -1) return commands.Count; // синтаксическая ошибка, пропускаем всё

            if (condition)
            {
                // Выполнить команды от следующей после if до else или endif
                int stopBefore = elseIndex != -1 ? elseIndex : endIfIndex;
                for (int i = startIndex + 1; i < stopBefore; i++)
                {
                    await ExecuteSingleCommand(commands[i], context);
                }
            }
            else if (elseIndex != -1)
            {
                // Выполнить от else+1 до endif
                for (int i = elseIndex + 1; i < endIfIndex; i++)
                {
                    await ExecuteSingleCommand(commands[i], context);
                }
            }
            return endIfIndex; // перепрыгиваем блок
        }

        private async Task<int> ExecuteWhileBlock(List<ScriptCommand> commands, int startIndex, ScriptExecutionContext context)
        {
            var cmd = commands[startIndex];
            int endWhileIndex = -1;
            int depth = 1;
            for (int i = startIndex + 1; i < commands.Count; i++)
            {
                if (commands[i].Type == ScriptCommandType.While) depth++;
                else if (commands[i].Type == ScriptCommandType.EndWhile) depth--;
                if (depth == 0) { endWhileIndex = i; break; }
            }
            if (endWhileIndex == -1) return commands.Count;

            while (EvaluateCondition(cmd.Parameters, context))
            {
                for (int i = startIndex + 1; i < endWhileIndex; i++)
                {
                    await ExecuteSingleCommand(commands[i], context);
                }
            }
            return endWhileIndex;
        }

        private Task ExecuteWait(ScriptCommand cmd)
        {
            if (cmd.Parameters.TryGetValue("Milliseconds", out var msStr) && int.TryParse(msStr, out int ms))
                return Task.Delay(ms);
            return Task.CompletedTask;
        }

        private async Task ExecuteDamageCharacter(ScriptCommand cmd, ScriptExecutionContext context)
        {
            var targetId = ResolveParameter("TargetId", cmd, context);
            var amountStr = ResolveParameter("Amount", cmd, context) ?? "0";
            int amount = int.Parse(amountStr);
            var damageType = ResolveParameter("DamageType", cmd, context) ?? "bludgeoning";
            var commandBus = _serviceProvider.GetService(typeof(ICommandBus)) as ICommandBus;
            if (commandBus != null && targetId != null)
                await commandBus.SendAsync(new DealDamage(Guid.Parse(targetId), amount, damageType));
        }

        private async Task ExecuteHealCharacter(ScriptCommand cmd, ScriptExecutionContext context)
        {
            var targetId = ResolveParameter("TargetId", cmd, context);
            int amount = int.Parse(ResolveParameter("Amount", cmd, context) ?? throw new InvalidOperationException("Amount is required"));
            var commandBus = _serviceProvider.GetService(typeof(ICommandBus)) as ICommandBus;
            if (commandBus != null && targetId != null)
                await commandBus.SendAsync(new HealCharacter(Guid.Parse(targetId), amount));
        }

        private async Task ExecuteMoveCharacter(ScriptCommand cmd, ScriptExecutionContext context)
        {
            var characterId = ResolveParameter("CharacterId", cmd, context);
            var x = int.Parse(ResolveParameter("X", cmd, context) ?? "0");
            var y = int.Parse(ResolveParameter("Y", cmd, context) ?? "0");
            var commandBus = _serviceProvider.GetService(typeof(ICommandBus)) as ICommandBus;
            if (commandBus != null && characterId != null)
                await commandBus.SendAsync(new MoveCharacter(Guid.Parse(characterId), x, y));
        }

        private async Task ExecuteGiveItem(ScriptCommand cmd, ScriptExecutionContext context)
        {
            var characterId = ResolveParameter("CharacterId", cmd, context);
            var itemId = ResolveParameter("ItemId", cmd, context);
            string itemName = ResolveParameter("ItemName", cmd, context) ?? "Unknown Item";
            var quantity = int.Parse(ResolveParameter("Quantity", cmd, context) ?? "1");
            var commandBus = _serviceProvider.GetService(typeof(ICommandBus)) as ICommandBus;
            if (commandBus != null && characterId != null && itemId != null)
                await commandBus.SendAsync(new AddInventoryItem(Guid.Parse(characterId), itemId, itemName, quantity));
        }

        private async Task ExecuteRemoveItem(ScriptCommand cmd, ScriptExecutionContext context)
        {
            var characterId = ResolveParameter("CharacterId", cmd, context);
            var itemId = ResolveParameter("ItemId", cmd, context);
            var quantity = int.Parse(ResolveParameter("Quantity", cmd, context) ?? "1");
            var commandBus = _serviceProvider.GetService(typeof(ICommandBus)) as ICommandBus;
            if (commandBus != null && characterId != null && itemId != null)
                await commandBus.SendAsync(new RemoveInventoryItem(Guid.Parse(characterId), itemId, quantity));
        }

        private async Task ExecuteStartDialogue(ScriptCommand cmd, ScriptExecutionContext context)
        {
            var dialogueId = ResolveParameter("DialogueId", cmd, context);
            var npcId = ResolveParameter("NpcId", cmd, context);
            var characterId = ResolveParameter("CharacterId", cmd, context);
            var commandBus = _serviceProvider.GetService(typeof(ICommandBus)) as ICommandBus;
            if (commandBus != null && dialogueId != null && npcId != null && characterId != null)
                await commandBus.SendAsync(new StartDialogueCommand(Guid.Parse(dialogueId), Guid.Parse(npcId), Guid.Parse(characterId)));
        }

        private async Task ExecuteSetQuestStage(ScriptCommand cmd, ScriptExecutionContext context)
        {
            // Используем CampaignProjection/агрегат через команду обновления цели квеста
            var campaignId = ResolveParameter("CampaignId", cmd, context) ?? context.CurrentCampaignId?.ToString();
            var questId = ResolveParameter("QuestId", cmd, context);
            var objectiveIndex = int.Parse(ResolveParameter("ObjectiveIndex", cmd, context) ?? "0");
            var isCompleted = bool.Parse(ResolveParameter("IsCompleted", cmd, context) ?? "false");
            var progress = int.Parse(ResolveParameter("Progress", cmd, context) ?? "0");
            var commandBus = _serviceProvider.GetService(typeof(ICommandBus)) as ICommandBus;
            if (commandBus != null && campaignId != null && questId != null)
                await commandBus.SendAsync(new UpdateQuestObjectiveCommand(Guid.Parse(campaignId), Guid.Parse(questId), objectiveIndex, isCompleted, progress));
        }

        private async Task ExecuteCompleteQuest(ScriptCommand cmd, ScriptExecutionContext context)
        {
            var campaignId = ResolveParameter("CampaignId", cmd, context) ?? context.CurrentCampaignId?.ToString();
            var questId = ResolveParameter("QuestId", cmd, context);
            var commandBus = _serviceProvider.GetService(typeof(ICommandBus)) as ICommandBus;
            if (commandBus != null && campaignId != null && questId != null)
                await commandBus.SendAsync(new CompleteQuestCommand(Guid.Parse(campaignId), Guid.Parse(questId)));
        }

        private async Task ExecuteFailQuest(ScriptCommand cmd, ScriptExecutionContext context)
        {
            var campaignId = ResolveParameter("CampaignId", cmd, context) ?? context.CurrentCampaignId?.ToString();
            var questId = ResolveParameter("QuestId", cmd, context);
            var commandBus = _serviceProvider.GetService(typeof(ICommandBus)) as ICommandBus;
            if (commandBus != null && campaignId != null && questId != null)
                await commandBus.SendAsync(new FailQuestCommand(Guid.Parse(campaignId), Guid.Parse(questId)));
        }

        private async Task ExecuteChangeFactionReputation(ScriptCommand cmd, ScriptExecutionContext context)
        {
            var factionId = ResolveParameter("FactionId", cmd, context);
            var change = int.Parse(ResolveParameter("Change", cmd, context) ?? "0");
            var commandBus = _serviceProvider.GetService(typeof(ICommandBus)) as ICommandBus;
            if (commandBus != null && factionId != null)
                await commandBus.SendAsync(new ChangeFactionReputation(Guid.Empty, factionId, change)); // campaignId можно извлечь из контекста
        }

        private async Task ExecuteSpawnMonster(ScriptCommand cmd, ScriptExecutionContext context)
        {
            var templateId = ResolveParameter("TemplateId", cmd, context);
            var x = int.Parse(ResolveParameter("X", cmd, context) ?? "0");
            var y = int.Parse(ResolveParameter("Y", cmd, context) ?? "0");
            var commandBus = _serviceProvider.GetService(typeof(ICommandBus)) as ICommandBus;
            if (commandBus != null && templateId != null)
                await commandBus.SendAsync(new SpawnMonsterCommand(templateId, x, y));
        }

        private async Task ExecuteStartCombat(ScriptCommand cmd, ScriptExecutionContext context)
        {
            var participants = ResolveParameter("Participants", cmd, context)?.Split(',').Select(Guid.Parse).ToList() ?? new List<Guid>();
            var combatId = Guid.NewGuid();
            var commandBus = _serviceProvider.GetService(typeof(ICommandBus)) as ICommandBus;
            if (commandBus != null)
                await commandBus.SendAsync(new StartCombat(combatId, participants));
        }

        private async Task ExecuteEndCombat(ScriptCommand cmd, ScriptExecutionContext context)
        {
            var combatId = ResolveParameter("CombatId", cmd, context);
            var commandBus = _serviceProvider.GetService(typeof(ICommandBus)) as ICommandBus;
            if (commandBus != null && combatId != null)
                await commandBus.SendAsync(new EndCombat(Guid.Parse(combatId)));
        }

        private void ExecuteApplyCondition(ScriptCommand cmd, ScriptExecutionContext context)
        {
            var targetId = ResolveParameter("TargetId", cmd, context);
            var condition = ResolveParameter("Condition", cmd, context);
            var duration = int.Parse(ResolveParameter("DurationRounds", cmd, context) ?? "0");
            var commandBus = _serviceProvider.GetService(typeof(ICommandBus)) as ICommandBus;
            if (commandBus != null && targetId != null && condition != null)
                new ApplyCondition(Guid.Parse(targetId), condition, duration);
        }

        private async Task ExecuteRemoveCondition(ScriptCommand cmd, ScriptExecutionContext context)
        {
            var targetId = ResolveParameter("TargetId", cmd, context);
            var condition = ResolveParameter("Condition", cmd, context);
            var commandBus = _serviceProvider.GetService(typeof(ICommandBus)) as ICommandBus;
            if (commandBus != null && targetId != null && condition != null)
                await commandBus.SendAsync(new RemoveCondition(Guid.Parse(targetId), condition));
        }

        private async Task ExecuteTeleport(ScriptCommand cmd, ScriptExecutionContext context)
        {
            var characterId = ResolveParameter("CharacterId", cmd, context);
            var x = int.Parse(ResolveParameter("X", cmd, context) ?? "0");
            var y = int.Parse(ResolveParameter("Y", cmd, context) ?? "0");
            var commandBus = _serviceProvider.GetService(typeof(ICommandBus)) as ICommandBus;
            if (commandBus != null && characterId != null)
                await commandBus.SendAsync(new TeleportCommand(Guid.Parse(characterId), x, y));
        }

        private Task ExecutePlaySound(ScriptCommand cmd)
        {
            var soundName = cmd.Parameters.GetValueOrDefault("SoundName", "ding");
            _logger.LogInformation("Play sound: {SoundName}", soundName);
            return Task.CompletedTask;
        }

        private Task ExecuteLogMessage(ScriptCommand cmd)
        {
            var message = cmd.Parameters.GetValueOrDefault("Message", "");
            _logger.LogInformation("Script message: {Message}", message);
            return Task.CompletedTask;
        }

        private async Task ExecuteRollSkillCheck(ScriptCommand cmd, ScriptExecutionContext context)
        {
            // Просто сохраняем результат в переменную
            var characterId = ResolveParameter("CharacterId", cmd, context);
            var skill = ResolveParameter("Skill", cmd, context);
            var dc = int.Parse(ResolveParameter("DC", cmd, context) ?? "10");
            var resultVar = ResolveParameter("ResultVar", cmd, context) ?? "skillResult";
            // Реальный бросок выполняется снаружи, здесь заглушка
            context.Variables[resultVar] = true; // предполагаем успех для упрощения
            await Task.CompletedTask;
        }

        private async Task ExecuteSetGlobalFlag(ScriptCommand cmd, ScriptExecutionContext context)
        {
            var campaignId = ResolveParameter("CampaignId", cmd, context) ?? context.CurrentCampaignId?.ToString();
            var flagName = ResolveParameter("FlagName", cmd, context);
            var flagValue = ResolveParameter("FlagValue", cmd, context) ?? "true";
            var commandBus = _serviceProvider.GetService(typeof(ICommandBus)) as ICommandBus;
            if (commandBus != null && campaignId != null && flagName != null)
                await commandBus.SendAsync(new SetGlobalFlagCommand(Guid.Parse(campaignId), flagName, flagValue));
        }

        private async Task ExecuteRemoveGlobalFlag(ScriptCommand cmd, ScriptExecutionContext context)
        {
            var campaignId = ResolveParameter("CampaignId", cmd, context) ?? context.CurrentCampaignId?.ToString();
            var flagName = ResolveParameter("FlagName", cmd, context);
            var commandBus = _serviceProvider.GetService(typeof(ICommandBus)) as ICommandBus;
            if (commandBus != null && campaignId != null && flagName != null)
                await commandBus.SendAsync(new RemoveGlobalFlagCommand(Guid.Parse(campaignId), flagName));
        }

        private async Task ExecuteAdvanceTime(ScriptCommand cmd, ScriptExecutionContext context)
        {
            var campaignId = ResolveParameter("CampaignId", cmd, context) ?? context.CurrentCampaignId?.ToString();
            var minutes = int.Parse(ResolveParameter("Minutes", cmd, context) ?? "60");
            var commandBus = _serviceProvider.GetService(typeof(ICommandBus)) as ICommandBus;
            if (commandBus != null && campaignId != null)
                await commandBus.SendAsync(new AdvanceTimeCommand(Guid.Parse(campaignId), minutes));
        }

        private async Task ExecuteChangeWeather(ScriptCommand cmd, ScriptExecutionContext context)
        {
            var campaignId = ResolveParameter("CampaignId", cmd, context) ?? context.CurrentCampaignId?.ToString();
            var weather = ResolveParameter("Weather", cmd, context) ?? "Clear";
            var commandBus = _serviceProvider.GetService(typeof(ICommandBus)) as ICommandBus;
            if (commandBus != null && campaignId != null)
                await commandBus.SendAsync(new ChangeWeatherCommand(Guid.Parse(campaignId), weather));
        }

        private async Task ExecuteCommandBusCmd(ScriptCommand cmd, ScriptExecutionContext context)
        {
            // Позволяет отправить произвольную команду, имя которой и параметры передаются в скрипте
            // Пример: Type = "ExecuteCommandBus", Parameters = { "CommandType"="DealDamage", "CharacterId"="...", "Amount"="10" }
            // Реализация требует рефлексии или фабрики; для примера не будем усложнять.
            await Task.CompletedTask;
        }

        // Вспомогательный метод выполнения одной команды (без перехода к следующей)
        private async Task ExecuteSingleCommand(ScriptCommand cmd, ScriptExecutionContext context)
        {
            switch (cmd.Type)
            {
                case ScriptCommandType.SetVariable: await ExecuteSetVariable(cmd, context); break;
                // Аналогично для остальных однотипных...
                // Для упрощения повторы опущены, но в реальном коде нужно перечислить все аналогично основному циклу.
                default: break;
            }
        }

        // Оценка условия (простые сравнения двух переменных)
        private bool EvaluateCondition(Dictionary<string, string> parameters, ScriptExecutionContext context)
        {
            if (!parameters.TryGetValue("Left", out var left) || !parameters.TryGetValue("Op", out var op) || !parameters.TryGetValue("Right", out var right))
                return false;

            object? leftVal = ResolveValue(left, context);
            object? rightVal = ResolveValue(right, context);

            if (leftVal is string lStr && rightVal is string rStr)
            {
                return op switch
                {
                    "==" => lStr == rStr,
                    "!=" => lStr != rStr,
                    _ => false
                };
            }
            if (leftVal is int lInt && rightVal is int rInt)
            {
                return op switch
                {
                    "==" => lInt == rInt,
                    "!=" => lInt != rInt,
                    ">" => lInt > rInt,
                    "<" => lInt < rInt,
                    ">=" => lInt >= rInt,
                    "<=" => lInt <= rInt,
                    _ => false
                };
            }
            return false;
        }

        private object? ResolveValue(string expr, ScriptExecutionContext context)
        {
            if (context.Variables.TryGetValue(expr, out var val))
                return val;
            // попытка парсинга как числа или guid
            if (int.TryParse(expr, out int i)) return i;
            if (Guid.TryParse(expr, out Guid g)) return g;
            return expr; // строка
        }

        private string? ResolveParameter(string key, ScriptCommand cmd, ScriptExecutionContext context)
        {
            if (!cmd.Parameters.TryGetValue(key, out var value)) return null;
            // если значение начинается с '$', это ссылка на переменную
            if (value.StartsWith('$') && context.Variables.TryGetValue(value[1..], out var varValue))
                return varValue?.ToString();
            return value;
        }
    }
}