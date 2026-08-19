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
    /// Представляет реплику NPC и набор возможных ответов игрока.
    /// </summary>
    public class DialogueNode
    {
        /// <summary>Уникальный идентификатор узла.</summary>
        public Guid NodeId { get; set; }

        /// <summary>Текст, произносимый NPC.</summary>
        public string NpcText { get; set; } = string.Empty;

        /// <summary>Доступные варианты ответа игрока.</summary>
        public List<DialogueOption> Options { get; set; } = [];

        /// <summary>Если <c>true</c>, диалог завершается при достижении этого узла.</summary>
        public bool IsExitNode { get; set; }
    }

    /// <summary>
    /// Вариант ответа игрока в диалоге.
    /// Может содержать условия видимости, проверку навыка и эффекты.
    /// </summary>
    public class DialogueOption
    {
        /// <summary>Уникальный идентификатор варианта ответа.</summary>
        public Guid OptionId { get; set; }

        /// <summary>Текст, который видит игрок.</summary>
        public string PlayerText { get; set; } = string.Empty;

        /// <summary>Идентификатор следующего узла. <c>null</c> — завершить диалог.</summary>
        public Guid? NextNodeId { get; set; }

        /// <summary>Условия видимости варианта ответа.</summary>
        public List<DialogueCondition>? Conditions { get; set; }

        /// <summary>Проверка навыка, если требуется для этого варианта.</summary>
        public DialogueCheck? SkillCheck { get; set; }

        /// <summary>Эффекты при успехе проверки (или если проверки нет).</summary>
        public List<DialogueEffect>? SuccessEffects { get; set; }

        /// <summary>Эффекты при провале проверки.</summary>
        public List<DialogueEffect>? FailureEffects { get; set; }
    }

    /// <summary>
    /// Условие отображения варианта ответа.
    /// Используется для скрытия или показа опций в зависимости от состояния персонажа или мира.
    /// </summary>
    public class DialogueCondition
    {
        /// <summary>
        /// Тип условия: "HasItem", "MinLevel", "ReputationAbove", "QuestCompleted", "FlagSet" и другие.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>Параметр условия (например, идентификатор предмета или флага).</summary>
        public string Parameter { get; set; } = string.Empty;

        /// <summary>Значение для сравнения (например, требуемый уровень или количество).</summary>
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// Проверка навыка / характеристики во время диалога.
    /// Определяет, какой бросок должен выполнить игрок и с какой сложностью.
    /// </summary>
    public class DialogueCheck
    {
        /// <summary>Название навыка или характеристики (например, "Persuasion", "Intimidation").</summary>
        public string SkillOrAbility { get; set; } = string.Empty;

        /// <summary>Сложность проверки (DC).</summary>
        public int DifficultyClass { get; set; }
    }

    /// <summary>
    /// Эффект (действие), выполняемое при выборе варианта ответа.
    /// Описывает изменение состояния: выдача предметов, изменение репутации, запуск квестов и т.д.
    /// </summary>
    public class DialogueEffect
    {
        /// <summary>Тип эффекта: "ChangeReputation", "GiveItem", "StartQuest", "SetFlag", "StartCombat" и др.</summary>
        public string EffectType { get; set; } = string.Empty;

        /// <summary>Параметры эффекта, специфичные для каждого типа.</summary>
        public Dictionary<string, string> Parameters { get; set; } = [];
    }

    /// <summary>
    /// Текущее состояние диалога для одного участника.
    /// Хранит информацию о текущем узле, посещённых узлах и активности диалога.
    /// </summary>
    public class DialogueState
    {
        /// <summary>Идентификатор активного диалога (соответствует идентификатору корневого узла или диалога).</summary>
        public Guid DialogueId { get; set; }

        /// <summary>Идентификатор NPC, участвующего в диалоге.</summary>
        public Guid NpcId { get; set; }

        /// <summary>Идентификатор персонажа игрока.</summary>
        public Guid CharacterId { get; set; }

        /// <summary>Идентификатор текущего узла диалога.</summary>
        public Guid CurrentNodeId { get; set; }

        /// <summary>Признак активности диалога.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Список идентификаторов посещённых узлов (для отслеживания прогресса).</summary>
        public List<Guid> VisitedNodeIds { get; set; } = [];
    }

    // ---------- Репозиторий диалогов ----------

    /// <summary>
    /// Репозиторий диалоговых деревьев.
    /// Предоставляет доступ к узлам диалогов по их идентификаторам.
    /// </summary>
    public interface IDialogueRepository
    {
        /// <summary>
        /// Возвращает корневой узел диалога.
        /// </summary>
        /// <param name="dialogueId">Идентификатор диалога.</param>
        /// <returns>Корневой узел или <c>null</c>, если диалог не найден.</returns>
        DialogueNode? GetRootNode(Guid dialogueId);

        /// <summary>
        /// Возвращает узел по идентификаторам диалога и узла.
        /// </summary>
        /// <param name="dialogueId">Идентификатор диалога.</param>
        /// <param name="nodeId">Идентификатор узла.</param>
        /// <returns>Узел или <c>null</c>, если не найден.</returns>
        DialogueNode? GetNode(Guid dialogueId, Guid nodeId);
    }

    // ---------- Сервис диалогов ----------

    /// <summary>
    /// Сервис для управления диалогами между персонажами игроков и NPC.
    /// Реализует логику начала, выбора вариантов, применения эффектов и завершения диалогов.
    /// Использует командную шину для изменения состояния и проекции для чтения данных персонажа.
    /// </summary>
    public class DialogService(
        ICommandBus commandBus,
        IDialogueRepository dialogueRepo,
        CharacterProjection characterProjection,
        PermissionChecker permissionChecker)
    {
        /// <summary>Хранилище активных диалогов (в памяти; в реальном проекте — БД или кэш).</summary>
        private readonly ConcurrentDictionary<Guid, DialogueState> _activeDialogues = new();

        /// <summary>
        /// Начать диалог между персонажем игрока и NPC.
        /// Проверяет права на управление персонажем, наличие диалога и его корневого узла,
        /// а также отсутствие уже активного диалога с этим NPC.
        /// </summary>
        /// <param name="dialogueId">Идентификатор диалогового дерева.</param>
        /// <param name="npcId">Идентификатор NPC.</param>
        /// <param name="characterId">Идентификатор персонажа игрока.</param>
        /// <returns>Состояние начатого диалога.</returns>
        /// <exception cref="UnauthorizedAccessException">Если у пользователя нет прав на управление персонажем.</exception>
        /// <exception cref="InvalidOperationException">Если диалог не найден или уже активен с этим NPC.</exception>
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
        /// Проверяет условия видимости, выполняет переход к следующему узлу или завершает диалог.
        /// Если вариант требует проверку навыка, метод выбрасывает исключение и ожидает вызова <see cref="ResolveSkillCheck"/>.
        /// </summary>
        /// <param name="dialogueId">Идентификатор активного диалога.</param>
        /// <param name="optionId">Идентификатор выбранного варианта ответа.</param>
        /// <returns>Обновлённое состояние диалога.</returns>
        /// <exception cref="InvalidOperationException">Если диалог не активен, узел/вариант не найден, условия не выполнены или требуется проверка навыка.</exception>
        /// <exception cref="UnauthorizedAccessException">Если у пользователя нет прав на управление персонажем.</exception>
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
        /// Вычисляет успешность, применяет соответствующие эффекты и выполняет переход.
        /// </summary>
        /// <param name="dialogueId">Идентификатор активного диалога.</param>
        /// <param name="optionId">Идентификатор варианта ответа с проверкой.</param>
        /// <param name="rollResult">Результат броска d20.</param>
        /// <param name="proficiencyBonus">Бонус мастерства персонажа.</param>
        /// <param name="abilityModifier">Модификатор соответствующей характеристики.</param>
        /// <returns>Обновлённое состояние диалога.</returns>
        /// <exception cref="InvalidOperationException">Если диалог не найден или вариант не требует проверки.</exception>
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
        /// <param name="dialogueId">Идентификатор активного диалога.</param>
        /// <returns>Текущий узел диалога или <c>null</c>, если диалог не активен или не найден.</returns>
        public DialogueNode? GetCurrentDialogueNode(Guid dialogueId)
        {
            if (!_activeDialogues.TryGetValue(dialogueId, out var state) || !state.IsActive)
                return null;

            return dialogueRepo.GetNode(state.DialogueId, state.CurrentNodeId);
        }

        /// <summary>
        /// Принудительно завершить диалог.
        /// </summary>
        /// <param name="dialogueId">Идентификатор активного диалога.</param>
        /// <exception cref="InvalidOperationException">Если диалог не найден.</exception>
        public async Task EndDialogue(Guid dialogueId)
        {
            if (!_activeDialogues.TryGetValue(dialogueId, out var state))
                throw new InvalidOperationException("No active dialogue.");

            await EndDialogueInternal(state);
        }

        // ---------- Приватные методы ----------

        /// <summary>
        /// Внутренняя логика завершения диалога: помечает состояние неактивным, удаляет из словаря
        /// и отправляет соответствующую команду.
        /// </summary>
        /// <param name="state">Состояние диалога для завершения.</param>
        private async Task EndDialogueInternal(DialogueState state)
        {
            state.IsActive = false;
            _activeDialogues.TryRemove(state.DialogueId, out _);
            await commandBus.SendAsync(new EndDialogueCommand(state.DialogueId));
        }

        /// <summary>
        /// Применяет эффект диалога, отправляя соответствующую команду через командную шину.
        /// Тип эффекта определяет, какая команда будет отправлена.
        /// </summary>
        /// <param name="state">Текущее состояние диалога (для получения идентификатора персонажа).</param>
        /// <param name="effect">Эффект для применения.</param>
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

        /// <summary>
        /// Проверяет условие видимости варианта ответа на основе данных персонажа из проекции.
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <param name="condition">Условие для проверки.</param>
        /// <returns><c>true</c>, если условие выполнено; иначе <c>false</c>.</returns>
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