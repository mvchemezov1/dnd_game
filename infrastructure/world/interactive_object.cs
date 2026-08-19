// infrastructure/world/interactive_object.cs
using dnd_game.domain.value_objects;
using dnd_game.Domain.Commands;
using dnd_game.Domain.Events;
using dnd_game.infrastructure.message_bus;

namespace dnd_game.Infrastructure.World
{
    /// <summary>
    /// Тип интерактивного объекта.
    /// </summary>
    public enum InteractiveObjectType
    {
        Door,
        Chest,
        Lever,
        Button,
        Trap,
        Altar,
        Portal,
        Sign,
        Container,
        Campfire,
        Throne,
        Well,
        Statue,
        Bookcase,
        HiddenPassage
    }

    /// <summary>
    /// Состояние объекта.
    /// </summary>
    public enum InteractiveObjectState
    {
        Closed,
        Open,
        Locked,
        Disarmed,
        Armed,
        Activated,
        Deactivated,
        Broken,
        Hidden,
        Revealed
    }

    /// <summary>
    /// Интерактивный объект игрового мира, соответствующий правилам DnD.
    /// Содержит данные для проверок навыков, условий взаимодействия и эффектов.
    /// </summary>
    public class InteractiveObject
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public InteractiveObjectType Type { get; set; }
        public InteractiveObjectState State { get; set; } = InteractiveObjectState.Closed;
        public Position Position { get; set; } = new(0, 0);

        // Условия взаимодействия
        public bool RequiresKey { get; set; }
        public string? RequiredKeyId { get; set; }
        public string? RequiredSpellId { get; set; }
        public string? RequiredQuestFlag { get; set; }          // глобальный флаг, который должен быть установлен
        public int MinimumStrength { get; set; }                // для силового открытия

        // Проверки навыков и DC
        public int LockpickDC { get; set; }                     // Взлом замка
        public int StrengthDC { get; set; }                     // Выбить дверь/поднять решётку
        public int DisarmTrapDC { get; set; }                   // Обезвреживание ловушки
        public int PerceptionDC { get; set; }                   // Заметить скрытый объект или ловушку
        public int InvestigationDC { get; set; }                // Обыскать, найти потайное отделение
        public int ArcanaDC { get; set; }                       // Понять магический механизм

        // Прочность и здоровье
        public int MaxHitPoints { get; set; } = 10;
        public int CurrentHitPoints { get; set; } = 10;
        public int ArmorClass { get; set; } = 15;
        public string DamageImmunities { get; set; } = string.Empty;  // "poison,psychic"
        public string DamageResistances { get; set; } = string.Empty;

        // Последствия взаимодействия
        public int DamageOnFail { get; set; }                   // урон при провале (ловушка)
        public string DamageTypeOnFail { get; set; } = "piercing";
        public string ConditionOnFail { get; set; } = string.Empty; // состояние, накладываемое при провале
        public string? ScriptNameOnOpen { get; set; }           // скрипт, запускаемый при открытии
        public string? ScriptNameOnFail { get; set; }           // скрипт при провале
        public string? SoundOnInteract { get; set; }            // "creaking_door", "click"

        // Награды
        public List<string> LootItemIds { get; set; } = new();
        public int Gold { get; set; }
        public int ExperiencePoints { get; set; }

        public int ConditionDurationRounds { get; set; } = 1;

        // ---------- Методы взаимодействия (создают команды) ----------

        /// <summary>
        /// Попытка открыть/использовать объект.
        /// </summary>
        public async Task<bool> TryOpen(Guid characterId, ICommandBus commandBus)
        {
            if (State == InteractiveObjectState.Open || State == InteractiveObjectState.Broken)
                return false;

            // Проверка требований
            if (RequiresKey)
            {
                // Проверяем наличие ключа у персонажа (через команду запроса? упрощённо – просто флаг)
                // Для примера: бросаем событие, что нужен ключ
                return false;
            }

            if (!string.IsNullOrEmpty(RequiredSpellId))
                return false; // нужно заклинание

            // Если заперто – требуется проверка
            if (State == InteractiveObjectState.Locked)
            {
                // Здесь должен быть вызван бросок ловкости (взлом) или силы. Возвращаем false, ожидая внешнего вызова.
                return false;
            }

            // Успех: меняем состояние
            State = InteractiveObjectState.Open;
            if (!string.IsNullOrEmpty(ScriptNameOnOpen))
                await commandBus.SendAsync(new TriggerScriptCommand(ScriptNameOnOpen, new Dictionary<string, object> { { "ObjectId", Id } }));
            GrantLoot(characterId, commandBus);
            return true;
        }

        /// <summary>
        /// Попытка взломать замок (ловкость рук).
        /// </summary>
        public async Task<string> AttemptPickLock(Guid characterId, int rollResult, int proficiencyBonus, int dexterityModifier, ICommandBus commandBus)
        {
            if (State != InteractiveObjectState.Locked) return "Not locked.";
            int total = rollResult + proficiencyBonus + dexterityModifier;
            if (total >= LockpickDC)
            {
                State = InteractiveObjectState.Closed;
                return "Lock picked.";
            }
            // Провал: может сломать отмычку, произвести шум и т.д.
            if (!string.IsNullOrEmpty(ScriptNameOnFail))
                await commandBus.SendAsync(new TriggerScriptCommand(ScriptNameOnFail, new Dictionary<string, object> { { "CharacterId", characterId } }));
            return "Lockpick failed.";
        }

        /// <summary>
        /// Попытка выбить дверь/поднять силой.
        /// </summary>
        public bool AttemptForce(Guid characterId, int athleticsCheck, ICommandBus commandBus)
        {
            if (State != InteractiveObjectState.Locked && State != InteractiveObjectState.Closed) return false;
            if (athleticsCheck >= StrengthDC)
            {
                State = InteractiveObjectState.Open;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Обезвредить ловушку.
        /// </summary>
        public async Task<bool> DisarmTrap(Guid characterId, int rollResult, int proficiencyBonus, int dexterityModifier, ICommandBus commandBus)
        {
            if (State != InteractiveObjectState.Armed) return false;
            int total = rollResult + proficiencyBonus + dexterityModifier;
            if (total >= DisarmTrapDC)
            {
                State = InteractiveObjectState.Disarmed;
                return true;
            }
            // Активация ловушки
            await ActivateTrap(characterId, commandBus);
            return false;
        }

        private async Task ActivateTrap(Guid characterId, ICommandBus commandBus)
        {
            if (DamageOnFail > 0)
                await commandBus.SendAsync(new DealDamage(characterId, DamageOnFail, DamageTypeOnFail));
            if (!string.IsNullOrEmpty(ConditionOnFail))
                await commandBus.SendAsync(new ApplyCondition(characterId, ConditionOnFail, ConditionDurationRounds));
            if (!string.IsNullOrEmpty(ScriptNameOnFail))
                await commandBus.SendAsync(new TriggerScriptCommand(ScriptNameOnFail, new Dictionary<string, object> { { "CharacterId", characterId } }));
        }

        /// <summary>
        /// Обыскать объект (проверка Внимания или Расследования).
        /// </summary>
        public string Search(Guid characterId, int perceptionRoll, int investigationRoll, ICommandBus commandBus)
        {
            if (State == InteractiveObjectState.Hidden && perceptionRoll >= PerceptionDC)
            {
                State = InteractiveObjectState.Revealed;
                return "You notice something unusual.";
            }
            if (investigationRoll >= InvestigationDC)
            {
                GrantLoot(characterId, commandBus);
                return "You found something!";
            }
            return "You find nothing.";
        }

        private void GrantLoot(Guid characterId, ICommandBus commandBus)
        {
            foreach (var itemId in LootItemIds)
                commandBus.SendAsync(new AddInventoryItem(characterId, itemId, itemId));
            if (Gold > 0)
                commandBus.SendAsync(new AddGold(characterId, Gold));
            if (ExperiencePoints > 0)
                commandBus.SendAsync(new GainExperience(characterId, ExperiencePoints));
        }

        /// <summary>
        /// Уничтожить объект.
        /// </summary>
        public bool Destroy(int damage, string damageType)
        {
            // Проверка на иммунитет
            if (DamageImmunities.Contains(damageType)) return false;
            if (DamageResistances.Contains(damageType)) damage /= 2;

            CurrentHitPoints -= damage;
            if (CurrentHitPoints <= 0)
            {
                State = InteractiveObjectState.Broken;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Репозиторий интерактивных объектов (хранение в памяти / БД).
    /// </summary>
    public interface IInteractiveObjectRepository
    {
        InteractiveObject? GetById(Guid id);
        List<InteractiveObject> GetAllInArea(int minX, int minY, int maxX, int maxY);
        void Add(InteractiveObject obj);
        void Remove(Guid id);
    }
}