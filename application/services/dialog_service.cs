// application/services/dialog_service.cs
using dnd_game.Domain.Commands;
using dnd_game.Domain.Events;
using dnd_game.Application.Projections;          // CharacterProjection
using dnd_game.Application.Security;            // PermissionChecker
using System.Collections.Concurrent;
using dnd_game.Domain.ValueObjects;
using dnd_game.Application.EventHandlers;
using dnd_game.infrastructure.message_bus;

namespace dnd_game.Application.Services
{
    // ---------- Модели данных диалога ----------

    /// <summary>
    /// Узел диалогового дерева.
    /// </summary>
    public class DialogueNode
    {
        public Guid NodeId { get; set; }
        public string NpcText { get; set; } = string.Empty;                // Текст, произносимый NPC
        public List<DialogueOption> Options { get; set; } = [];         // Доступные варианты ответа игрока
        public bool IsExitNode { get; set; }                               // Завершает диалог при достижении
    }

    /// <summary>
    /// Вариант ответа игрока.
    /// </summary>
    public class DialogueOption
    {
        public Guid OptionId { get; set; }
        public string PlayerText { get; set; } = string.Empty;             // Текст, который видит игрок
        public Guid? NextNodeId { get; set; }                              // Следующий узел (null — остаться на текущем / завершить)
        public List<DialogueCondition>? Conditions { get; set; }           // Условия видимости варианта
        public DialogueCheck? SkillCheck { get; set; }                     // Проверка навыка, если требуется
        public List<DialogueEffect>? SuccessEffects { get; set; }          // Эффекты при успехе проверки (или если проверки нет)
        public List<DialogueEffect>? FailureEffects { get; set; }          // Эффекты при провале проверки
    }

    /// <summary>
    /// Условие отображения варианта ответа.
    /// </summary>
    public class DialogueCondition
    {
        public string Type { get; set; } = string.Empty;                   // "HasItem", "MinLevel", "ReputationAbove", "QuestCompleted", "FlagSet", ...
        public string Parameter { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// Проверка навыка / характеристики во время диалога.
    /// </summary>
    public class DialogueCheck
    {
        public string SkillOrAbility { get; set; } = string.Empty;         // "Persuasion", "Intimidation", "Deception", "Insight", "Charisma", ...
        public int DifficultyClass { get; set; }                           // DC проверки
    }

    /// <summary>
    /// Эффект (действие), выполняемое при выборе варианта.
    /// </summary>
    public class DialogueEffect
    {
        public string EffectType { get; set; } = string.Empty;             // "ChangeReputation", "GiveItem", "StartQuest", "SetFlag", "StartCombat", ...
        public Dictionary<string, string> Parameters { get; set; } = [];
    }

    /// <summary>
    /// Текущее состояние диалога для одного участника.
    /// </summary>
    public class DialogueState
    {
        public Guid DialogueId { get; set; }
        public Guid NpcId { get; set; }
        public Guid CharacterId { get; set; }
        public Guid CurrentNodeId { get; set; }
        public bool IsActive { get; set; } = true;
        public List<Guid> VisitedNodeIds { get; set; } = [];
    }

    // ---------- Репозиторий диалогов ----------

    public interface IDialogueRepository
    {
        DialogueNode? GetRootNode(Guid dialogueId);
        DialogueNode? GetNode(Guid dialogueId, Guid nodeId);
    }

    // ---------- Сервис диалогов ----------

    public class DialogService(
        ICommandBus commandBus,
        IDialogueRepository dialogueRepo,
        CharacterProjection characterProjection,
        PermissionChecker permissionChecker)
    {

        // Хранение активных состояний (в памяти, в реальном проекте – в БД или кэше)
        private readonly ConcurrentDictionary<Guid, DialogueState> _activeDialogues = new();

        /// <summary>
        /// Начать диалог между персонажем игрока и NPC.
        /// </summary>
        public async Task<DialogueState> StartDialogue(Guid dialogueId, Guid npcId, Guid characterId)
        {
            // Проверка прав: игрок может управлять своим персонажем; мастер может начинать диалог за любого
            if (!permissionChecker.CanControlCharacter(characterId))
                throw new UnauthorizedAccessException("You cannot control this character.");

            var rootNode = dialogueRepo.GetRootNode(dialogueId)
                           ?? throw new InvalidOperationException("Dialogue not found or has no root node.");

            // Проверяем, не ведёт ли уже персонаж диалог с этим NPC
            var existing = _activeDialogues.Values.FirstOrDefault(d => d.CharacterId == characterId && d.NpcId == npcId);
            if (existing != null)
                throw new InvalidOperationException("Character is already in a dialogue with this NPC.");

            var state = new DialogueState
            {
                DialogueId = dialogueId,
                NpcId = npcId,
                CharacterId = characterId,
                CurrentNodeId = rootNode.NodeId,
                IsActive = true
            };
            state.VisitedNodeIds.Add(rootNode.NodeId);
            _activeDialogues[state.DialogueId] = state; // используем dialogueId как ключ (упрощение: одна активная сессия на диалог)

            // Отправить событие о начале диалога
            await commandBus.SendAsync(new StartDialogueCommand(dialogueId, npcId, characterId));
            return state;
        }

        /// <summary>
        /// Выбрать вариант ответа в активном диалоге.
        /// Возвращает обновлённое состояние диалога.
        /// </summary>
        public async Task<DialogueState> SelectOption(Guid dialogueId, Guid optionId)
        {
            if (!_activeDialogues.TryGetValue(dialogueId, out var state))
                throw new InvalidOperationException("No active dialogue with this ID.");

            if (!state.IsActive)
                throw new InvalidOperationException("Dialogue is already finished.");

            // Проверка прав
            if (!permissionChecker.CanControlCharacter(state.CharacterId))
                throw new UnauthorizedAccessException("You cannot control this character.");

            var currentNode = dialogueRepo.GetNode(state.DialogueId, state.CurrentNodeId)
                              ?? throw new InvalidOperationException("Current dialogue node not found.");

            var selectedOption = currentNode.Options.FirstOrDefault(o => o.OptionId == optionId)
                                 ?? throw new InvalidOperationException("Option not available.");

            // Проверка условий видимости (если есть)
            if (selectedOption.Conditions != null)
            {
                foreach (var condition in selectedOption.Conditions)
                {
                    if (!await EvaluateCondition(state.CharacterId, condition))
                        throw new InvalidOperationException("Option conditions not met.");
                }
            }

            // Выполнение проверки навыка, если задана
            bool checkSuccess = true; // по умолчанию проверки нет или она не требуется
            if (selectedOption.SkillCheck != null)
            {
                // Отправляем команду на бросок навыка (обработчик вернёт результат, но здесь для простоты синхронно не ждём).
                // В реальной архитектуре диалог может приостанавливаться до получения результата.
                // Для примера используем заглушку: предположим, что бросок уже сделан и передан вместе с командой выбора опции.
                // Мы должны были бы расширить команду SelectOption дополнительными параметрами (rollResult).
                // Здесь оставим упрощённую логику: если требуется проверка, мы ожидаем, что результат уже известен и передан,
                // или вызываем отдельный метод.
                throw new InvalidOperationException("Skill checks during dialogue must be resolved via ResolveSkillCheck method.");
            }

            // Применяем эффекты в зависимости от успеха проверки
            var effects = checkSuccess ? selectedOption.SuccessEffects : selectedOption.FailureEffects;
            if (effects != null)
            {
                foreach (var effect in effects)
                    await ApplyEffect(state, effect);
            }

            // Переход к следующему узлу или завершение
            if (selectedOption.NextNodeId.HasValue)
            {
                state.CurrentNodeId = selectedOption.NextNodeId.Value;
                state.VisitedNodeIds.Add(selectedOption.NextNodeId.Value);

                var nextNode = dialogueRepo.GetNode(state.DialogueId, selectedOption.NextNodeId.Value);
                if (nextNode != null && nextNode.IsExitNode)
                {
                    await EndDialogueInternal(state);
                }
            }
            else
            {
                // Если следующего узла нет, диалог завершается
                await EndDialogueInternal(state);
            }

            return state;
        }

        /// <summary>
        /// Разрешить проверку навыка в диалоге. Вызывается после того, как бросок сделан.
        /// </summary>
        public async Task<DialogueState> ResolveSkillCheck(Guid dialogueId, Guid optionId, int rollResult, int proficiencyBonus, int abilityModifier)
        {
            if (!_activeDialogues.TryGetValue(dialogueId, out var state))
                throw new InvalidOperationException("No active dialogue.");

            var currentNode = dialogueRepo.GetNode(state.DialogueId, state.CurrentNodeId)!;
            var option = currentNode.Options.First(o => o.OptionId == optionId);
            if (option.SkillCheck == null)
                throw new InvalidOperationException("Option does not require a skill check.");

            int total = rollResult + proficiencyBonus + abilityModifier;
            bool success = total >= option.SkillCheck.DifficultyClass;

            // Применяем эффекты
            var effects = success ? option.SuccessEffects : option.FailureEffects;
            if (effects != null)
                foreach (var e in effects)
                    await ApplyEffect(state, e);

            // Переход
            if (option.NextNodeId.HasValue)
            {
                state.CurrentNodeId = option.NextNodeId.Value;
                state.VisitedNodeIds.Add(option.NextNodeId.Value);
                var nextNode = dialogueRepo.GetNode(state.DialogueId, option.NextNodeId.Value);
                if (nextNode != null && nextNode.IsExitNode)
                    await EndDialogueInternal(state);
            }
            else
            {
                await EndDialogueInternal(state);
            }

            return state;
        }

        /// <summary>
        /// Получить текущее состояние диалога (текст NPC, варианты ответов).
        /// </summary>
        public DialogueNode? GetCurrentDialogueNode(Guid dialogueId)
        {
            if (!_activeDialogues.TryGetValue(dialogueId, out var state) || !state.IsActive)
                return null;

            return dialogueRepo.GetNode(state.DialogueId, state.CurrentNodeId);
        }

        /// <summary>
        /// Принудительно завершить диалог.
        /// </summary>
        public async Task EndDialogue(Guid dialogueId)
        {
            if (!_activeDialogues.TryGetValue(dialogueId, out var state))
                throw new InvalidOperationException("No active dialogue.");

            await EndDialogueInternal(state);
        }

        // ---------- Приватные методы ----------

        private async Task EndDialogueInternal(DialogueState state)
        {
            state.IsActive = false;
            _activeDialogues.TryRemove(state.DialogueId, out _);
            await commandBus.SendAsync(new EndDialogueCommand(state.DialogueId));
        }

        private async Task ApplyEffect(DialogueState state, DialogueEffect effect)
        {
            switch (effect.EffectType)
            {
                case "ChangeReputation":
                    {
                        var factionId = effect.Parameters["FactionId"];
                        int delta = int.Parse(effect.Parameters["Amount"]);
                        await commandBus.SendAsync(new ChangeFactionReputation(state.CharacterId, factionId, delta));
                        break;
                    }
                case "GiveItem":
                    {
                        string itemId = effect.Parameters["ItemId"];
                        string itemName = effect.Parameters.GetValueOrDefault("ItemName", itemId);
                        int quantity = effect.Parameters.TryGetValue("Quantity", out string? qtyStr) ? int.Parse(qtyStr!) : 1;
                        await commandBus.SendAsync(new AddInventoryItem(state.CharacterId, itemId, itemName, quantity));
                        break;
                    }
                case "RemoveItem":
                    {
                        string removeItemId = effect.Parameters["ItemId"];
                        await commandBus.SendAsync(new RemoveInventoryItem(state.CharacterId, removeItemId));
                        break;
                    }
                case "StartQuest":
                    {
                        var questId = Guid.Parse(effect.Parameters["QuestId"]);
                        await commandBus.SendAsync(new StartQuestCommand(state.CharacterId, questId));
                        break;
                    }
                case "CompleteQuest":
                    {
                        var completeQuestId = Guid.Parse(effect.Parameters["QuestId"]);
                        await commandBus.SendAsync(new CompleteQuestCommand(state.CharacterId, completeQuestId));
                        break;
                    }
                case "SetFlag":
                    {
                        string flagName = effect.Parameters["Flag"];
                        string flagValue = effect.Parameters["Value"];
                        Guid campaignId = effect.Parameters.TryGetValue("CampaignId", out string? cidStr) && Guid.TryParse(cidStr, out var cid) ? cid : Guid.Empty;
                        await commandBus.SendAsync(new SetGlobalFlagCommand(campaignId, flagName, flagValue));
                        break;
                    }
                case "StartCombat":
                    {
                        await commandBus.SendAsync(new StartCombat(Guid.NewGuid(), [state.CharacterId, state.NpcId]));
                        break;
                    }
                case "Heal":
                    {
                        int healAmount = int.Parse(effect.Parameters["Amount"]);
                        await commandBus.SendAsync(new HealCharacter(state.CharacterId, healAmount));
                        break;
                    }
                default:
                    // неизвестный эффект – можно залогировать
                    break;
            }
        }

        private async Task<bool> EvaluateCondition(Guid characterId, DialogueCondition condition)
        {
            var character = await characterProjection.GetById(characterId);
            if (character == null) return false;

            return condition.Type switch
            {
                "HasItem" => character.Inventory.Any(i => i.ItemId == condition.Parameter),
                "MinLevel" => character.Level >= int.Parse(condition.Value),
                "QuestCompleted" => true, // нужна проверка состояния квестов через CampaignProjection
                "ReputationAbove" => true, // аналогично
                "FlagSet" => true,        // проверка глобальных флагов
                _ => false
            };
        }
    }
}