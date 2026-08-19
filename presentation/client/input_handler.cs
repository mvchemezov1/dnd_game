// presentation/client/input_handler.cs
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using dnd_game.Domain.Commands;
using dnd_game.Infrastructure.Network;
using Microsoft.Extensions.Logging;

namespace dnd_game.Presentation.Client
{
    /// <summary>
    /// Режим ввода, определяющий доступные команды.
    /// </summary>
    public enum InputMode
    {
        Normal,      // вне боя: разговор, исследование, отдых
        Combat,      // боевой режим: ограниченный набор действий
        TargetSelection, // выбор цели для заклинания/атаки
        Dialogue,    // диалог с NPC (выбор вариантов ответа)
        Inventory,   // управление инвентарём
        Spellbook,   // просмотр и подготовка заклинаний
        Crafting     // крафт
    }

    /// <summary>
    /// Результат обработки ввода.
    /// </summary>
    public class InputResult
    {
        public bool Success { get; set; }
        public ICommand? Command { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Привязка клавиш (key bindings) для быстрых действий.
    /// </summary>
    public class KeyBindings
    {
        public Dictionary<ConsoleKey, string> Bindings { get; set; } = new()
        {
            { ConsoleKey.A, "attack" },
            { ConsoleKey.M, "move" },
            { ConsoleKey.D, "dash" },
            { ConsoleKey.G, "disengage" },
            { ConsoleKey.H, "hide" },
            { ConsoleKey.I, "inventory" },
            { ConsoleKey.S, "spells" },
            { ConsoleKey.C, "character" },
            { ConsoleKey.R, "rest" },
            { ConsoleKey.E, "end_turn" },
            { ConsoleKey.Enter, "confirm" },
            { ConsoleKey.Escape, "cancel" }
        };
    }

    /// <summary>
    /// Обработчик ввода, преобразующий текстовые команды и нажатия клавиш в доменные команды DnD.
    /// </summary>
    public class InputHandler(IGameClient client, KeyBindings? keyBindings = null, ILogger<InputHandler>? logger = null)
    {
        private readonly KeyBindings _keyBindings = keyBindings ?? new KeyBindings();
        private readonly ILogger<InputHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Текущий контекст игрока
        public InputMode CurrentMode { get; set; } = InputMode.Normal;
        public Guid ControlledCharacterId { get; set; }
        public Guid? ActiveCombatId { get; set; }
        public Guid? ActiveDialogueId { get; set; }

        // Словари псевдонимов и команд
        private static readonly Dictionary<string, string> CommandAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            { "a", "attack" },
            { "m", "move" },
            { "atk", "attack" },
            { "mv", "move" },
            { "dsh", "dash" },
            { "dng", "disengage" },
            { "hde", "hide" },
            { "inv", "inventory" },
            { "spl", "spells" },
            { "chr", "character" },
            { "rst", "rest" },
            { "end", "end_turn" },
            { "loot", "take_all" },
            { "eq", "equip" },
            { "uneq", "unequip" },
            { "use", "use_item" },
            { "drop", "drop_item" },
            { "look", "examine" },
            { "talk", "speak" }
        };

        /// <summary>
        /// Обработать текстовый ввод (чат-команда или консоль).
        /// </summary>
        public async Task<InputResult> ProcessInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new InputResult { Success = false, Message = "Empty input." };

            // Если в режиме диалога, ввод означает выбор варианта ответа или текст
            if (CurrentMode == InputMode.Dialogue)
                return await ProcessDialogueInput(input);

            // Если в режиме выбора цели, ввод – это координаты или имя цели
            if (CurrentMode == InputMode.TargetSelection)
                return ProcessTargetSelection(input);

            // Разбор основной команды
            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return new InputResult { Success = false };

            string commandName = ResolveAlias(parts[0]);
            var args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

            return commandName.ToLowerInvariant() switch
            {
                "attack" => await HandleAttack(args),
                "move" => await HandleMove(args),
                "dash" => BuildCommand(new MoveCharacterWithDash(ControlledCharacterId)),
                "disengage" => BuildCommand(new MoveCharacterWithDisengage(ControlledCharacterId)),
                "hide" => BuildCommand(new MoveCharacterStealthily(ControlledCharacterId)),
                "cast" => await HandleCastSpell(args),
                "heal" => await HandleHeal(args),
                "rest" => await HandleRest(args),
                "use_item" => await HandleUseItem(args),
                "equip" => await HandleEquip(args),
                "unequip" => await HandleUnequip(args),
                "examine" => HandleExamine(args),
                "take_all" => await HandleTakeAll(args),
                "drop_item" => await HandleDropItem(args),
                "speak" => await HandleSpeak(args),
                "character" => BuildResponse("Character sheet requested. (UI toggle)"),
                "inventory" => BuildResponse("Inventory display requested."),
                "spells" => BuildResponse("Spellbook display requested."),
                "end_turn" => await HandleEndTurn(),
                "cancel" => BuildResponse("Action cancelled."),
                "help" => BuildResponse(GetHelpText()),
                _ => new InputResult { Success = false, Message = $"Unknown command: '{commandName}'. Type 'help' for available commands." }
            };
        }

        /// <summary>
        /// Обработать нажатие клавиши (быстрое действие).
        /// </summary>
        public async Task<InputResult> ProcessKey(ConsoleKey key)
        {
            if (_keyBindings.Bindings.TryGetValue(key, out var command))
            {
                return await ProcessInput(command);
            }
            return new InputResult { Success = false, Message = $"No binding for key {key}." };
        }

        // ---------- Обработчики конкретных команд ----------

        private async Task<InputResult> HandleAttack(string[] args)
        {
            if (CurrentMode != InputMode.Combat)
                return new InputResult { Success = false, Message = "You can only attack during combat." };

            // attack [target_id or name] [melee/ranged]
            Guid targetId = args.Length > 0 && Guid.TryParse(args[0], out var tid) ? tid : Guid.Empty;
            if (targetId == Guid.Empty)
            {
                // Переключиться в режим выбора цели
                CurrentMode = InputMode.TargetSelection;
                return new InputResult { Success = false, Message = "Select a target (click or type target ID)." };
            }

            string actionType = args.Length > 1 && args[1].Equals("ranged", StringComparison.OrdinalIgnoreCase)
                ? "RangedAttack" : "Attack";
            var cmd = new TakeStandardAction(ActiveCombatId ?? Guid.Empty, ControlledCharacterId, actionType, targetId);
            return await SendCommandAsync(cmd);
        }

        private async Task<InputResult> HandleMove(string[] args)
        {
            // move <x> <y>    или    move <direction> <distance>
            int x = 0, y = 0;
            if (args.Length >= 2 && int.TryParse(args[0], out x) && int.TryParse(args[1], out y))
            {
                var cmd = new MoveCharacter(ControlledCharacterId, x, y);
                return await SendCommandAsync(cmd);
            }
            // Поддержка направлений (north, south, east, west, ne, nw, se, sw) и расстояния
            if (args.Length >= 2)
            {
                int distance = int.TryParse(args[1], out var d) ? d : 5;
                (int dx, int dy) = args[0].ToLower() switch
                {
                    "north" or "n" => (0, distance),
                    "south" or "s" => (0, -distance),
                    "east" or "e" => (distance, 0),
                    "west" or "w" => (-distance, 0),
                    "ne" or "northeast" => (distance, distance),
                    "nw" or "northwest" => (-distance, distance),
                    "se" or "southeast" => (distance, -distance),
                    "sw" or "southwest" => (-distance, -distance),
                    _ => (0, 0)
                };
                var cmd = new MoveCharacter(ControlledCharacterId, dx, dy);
                return await SendCommandAsync(cmd);
            }
            return new InputResult { Success = false, Message = "Usage: move <x> <y> or move <direction> <distance>" };
        }

        private async Task<InputResult> HandleCastSpell(string[] args)
        {
            if (args.Length < 1)
                return new InputResult { Success = false, Message = "Usage: cast <spell_id> [target] [slot_level]" };

            string spellId = args[0];
            // Если есть цель, ищем её
            Guid? targetId = null;
            if (args.Length > 1 && Guid.TryParse(args[1], out var tId))
                targetId = tId;

            // Если нужно выбрать цель (не указана), переходим в режим выбора
            if (targetId == null && CurrentMode == InputMode.Combat)
            {
                CurrentMode = InputMode.TargetSelection;
                return new InputResult { Success = false, Message = $"Select target for {spellId}." };
            }

            int slotLevel = args.Length > 2 ? int.Parse(args[2]) : 1;
            var cmd = new CastSpell(ControlledCharacterId, spellId, targetId, slotLevel);
            return await SendCommandAsync(cmd);
        }

        private async Task<InputResult> HandleHeal(string[] args)
        {
            int amount = args.Length > 0 ? int.Parse(args[0]) : 0;
            if (amount <= 0) return new InputResult { Success = false, Message = "Usage: heal <amount>" };
            return await SendCommandAsync(new HealCharacter(ControlledCharacterId, amount));
        }

        private async Task<InputResult> HandleRest(string[] args)
        {
            string restType = args.Length > 0 && args[0].Equals("long", StringComparison.OrdinalIgnoreCase)
                ? "Long" : "Short";
            return await SendCommandAsync(new StartRest(ControlledCharacterId, restType));
        }

        private async Task<InputResult> HandleUseItem(string[] args)
        {
            if (args.Length < 1) return new InputResult { Success = false, Message = "Usage: use_item <item_id>" };
            string itemId = args[0];
            // Использование предмета может быть разным: зелье, свиток и т.д. Упрощённо – отправляем UseItem
            return await SendCommandAsync(new UseItem(ControlledCharacterId, itemId));
        }

        private async Task<InputResult> HandleEquip(string[] args)
        {
            if (args.Length < 2) return new InputResult { Success = false, Message = "Usage: equip <item_id> <slot>" };
            return await SendCommandAsync(new EquipItem(ControlledCharacterId, args[0], args[1], args[0]));
        }

        private async Task<InputResult> HandleUnequip(string[] args)
        {
            if (args.Length < 1) return new InputResult { Success = false, Message = "Usage: unequip <item_id>" };
            return await SendCommandAsync(new UnequipItem(ControlledCharacterId, args[0]));
        }

        private InputResult HandleExamine(string[] args)
        {
            if (args.Length < 1) return new InputResult { Success = false, Message = "Usage: examine <object_id>" };
            // Для осмотра объекта можно отправить запрос (query) на получение описания.
            // Упрощённо: возвращаем сообщение.
            return BuildResponse($"You examine {args[0]}.");
        }

        private async Task<InputResult> HandleTakeAll(string[] args)
        {
            // Взять всё из контейнера / с трупа
            return await SendCommandAsync(new LootAll(ControlledCharacterId));
        }

        private async Task<InputResult> HandleDropItem(string[] args)
        {
            if (args.Length < 1) return new InputResult { Success = false, Message = "Usage: drop <item_id> [quantity]" };
            int qty = args.Length > 1 && int.TryParse(args[1], out var q) ? q : 1;
            return await SendCommandAsync(new RemoveInventoryItem(ControlledCharacterId, args[0], qty));
        }

        private async Task<InputResult> HandleSpeak(string[] args)
        {
            if (args.Length < 1) return new InputResult { Success = false, Message = "Usage: speak <message>" };
            // Отправляем сообщение в чат или начинаем диалог с NPC
            // Здесь просто заглушка: отправляем команду Speak
            return await SendCommandAsync(new SpeakCommand(ControlledCharacterId, string.Join(" ", args)));
        }

        private async Task<InputResult> HandleEndTurn()
        {
            if (ActiveCombatId == null) return new InputResult { Success = false, Message = "Not in combat." };
            return await SendCommandAsync(new NextTurn(ActiveCombatId.Value));
        }

        // ---------- Обработка ввода в специальных режимах ----------

        private async Task<InputResult> ProcessDialogueInput(string input)
        {
            // Ввод номера варианта ответа или текст свободного ввода
            if (int.TryParse(input, out int optionIndex))
            {
                // Отправляем команду выбора опции диалога
                return await SendCommandAsync(new SelectDialogueOption(ActiveDialogueId ?? Guid.Empty, optionIndex));
            }
            // Иначе свободный текст (если NPC принимает текстовые ответы)
            return await SendCommandAsync(new DialogueTextInput(ActiveDialogueId ?? Guid.Empty, input));
        }

        private InputResult ProcessTargetSelection(string input)
        {
            // Ожидаем координаты или идентификатор цели
            if (Guid.TryParse(input, out var targetId))
            {
                CurrentMode = InputMode.Combat;
                // Повторно вызываем последнюю команду с указанной целью (можно сохранить ожидающую команду в поле)
                return new InputResult { Success = true, Message = $"Target {targetId} selected. Re-issue your action." };
            }
            return new InputResult { Success = false, Message = "Invalid target. Enter a valid target ID." };
        }

        // ---------- Вспомогательные методы ----------

        private string ResolveAlias(string input) =>
            CommandAliases.TryGetValue(input, out var resolved) ? resolved : input;

        private async Task<InputResult> SendCommandAsync(ICommand command)
        {
            try
            {
                await client.SendCommandAsync(command);
                return new InputResult { Success = true, Command = command, Message = "Command sent." };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send command {CommandType}", command.GetType().Name);
                return new InputResult { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        private InputResult BuildCommand(ICommand command) =>
            new() { Success = true, Command = command, Message = $"{command.GetType().Name} ready." };

        private static InputResult BuildResponse(string message) =>
            new() { Success = true, Message = message };

        private string GetHelpText()
        {
            return CurrentMode switch
            {
                InputMode.Combat => "Combat commands: attack, move, dash, disengage, hide, cast, use_item, end_turn",
                InputMode.Normal => "Commands: move, rest, inventory, spells, equip, unequip, use, drop, look, speak, help",
                _ => "Available commands: help, cancel"
            };
        }
    }
}