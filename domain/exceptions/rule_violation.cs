// domain/exceptions/rule_violation.cs
namespace dnd_game.Domain.Exceptions
{
    /// <summary>
    /// Исключение, сигнализирующее о нарушении одного или нескольких правил Dungeons and Dragons.
    /// Содержит подробный контекст, позволяющий обработчикам (например, UI) отобразить
    /// понятное сообщение и, при необходимости, предложить способы исправления.
    /// </summary>
    public class RuleViolation(string ruleName, string message) : DomainError($"Rule '{ruleName}' violated: {message}")
    {
        /// <summary>Краткое название правила (например, "Concentration", "ActionEconomy", "AttunementSlots").</summary>
        public string RuleName { get; } = ruleName;

        /// <summary>Идентификатор персонажа, нарушившего правило (если применимо).</summary>
        public Guid? CharacterId { get; }

        /// <summary>Идентификатор связанного объекта (предмета, заклинания, боя).</summary>
        public Guid? RelatedEntityId { get; }

        /// <summary>Человекочитаемое описание того, что именно пошло не так.</summary>
        public string ViolationDescription { get; } = message;

        /// <summary>Ссылка на соответствующий раздел правил (например, "PHB p.203", "DMG p.141").</summary>
        public string? RuleReference { get; }

        /// <summary>Список предлагаемых действий для устранения нарушения (может быть пустым).</summary>
        public List<string> SuggestedActions { get; } = [];

        public RuleViolation(string ruleName, string message, string? ruleReference)
            : this(ruleName, message)
        {
            RuleReference = ruleReference;
        }

        public RuleViolation(Guid characterId, string ruleName, string message)
            : this(ruleName, message)
        {
            CharacterId = characterId;
        }

        public RuleViolation(Guid characterId, string ruleName, string message, string? ruleReference)
            : this(characterId, ruleName, message)
        {
            RuleReference = ruleReference;
        }

        public RuleViolation(Guid characterId, Guid relatedEntityId, string ruleName, string message, string? ruleReference = null)
            : this(characterId, ruleName, message, ruleReference)
        {
            RelatedEntityId = relatedEntityId;
        }

        // ---------- Статические фабричные методы для типичных нарушений ----------

        /// <summary>
        /// Попытка поддерживать концентрацию на двух заклинаниях одновременно.
        /// </summary>
        public static RuleViolation ConcentrationConflict(Guid characterId, string existingSpell, string newSpell)
            => new(characterId, "Concentration",
                $"Cannot concentrate on '{newSpell}' while already concentrating on '{existingSpell}'.",
                "PHB p.203");

        /// <summary>
        /// Использование двух заклинаний со временем накладывания «1 бонусное действие» и
        /// «1 действие» в один ход без соблюдения ограничений.
        /// </summary>
        public static RuleViolation BonusActionSpellRestriction(Guid characterId)
            => new(characterId, "Spellcasting",
                "When casting a spell as a bonus action, you can only cast a cantrip with your action.",
                "PHB p.202");

        /// <summary>
        /// Превышение количества ячеек заклинаний.
        /// </summary>
        public static RuleViolation NoSpellSlotsAvailable(Guid characterId, int slotLevel)
            => new(characterId, "SpellSlots",
                $"No available spell slots of level {slotLevel}.",
                "PHB p.201");

        /// <summary>
        /// Попытка использовать больше одного основного действия за ход.
        /// </summary>
        public static RuleViolation ExtraActionNotAllowed(Guid characterId)
            => new(characterId, "ActionEconomy",
                "You cannot take more than one action per turn unless you have a feature that allows it (e.g., Action Surge).",
                "PHB p.189");

        /// <summary>
        /// Попытка носить два предмета в одном слоте экипировки.
        /// </summary>
        public static RuleViolation EquipmentSlotConflict(Guid characterId, string slot, string existingItem, string newItem)
            => new(characterId, "Equipment",
                $"Cannot equip '{newItem}' in slot '{slot}' because '{existingItem}' is already equipped there.",
                "PHB p.143");

        /// <summary>
        /// Превышение лимита аттунемента (3 магических предмета).
        /// </summary>
        public static RuleViolation AttunementLimitExceeded(Guid characterId)
            => new(characterId, "Attunement",
                "A character can be attuned to no more than three magic items at a time.",
                "DMG p.138");

        /// <summary>
        /// Попытка длительного отдыха чаще одного раза в 24 часа.
        /// </summary>
        public static RuleViolation LongRestCooldown(Guid characterId)
            => new(characterId, "Rest",
                "A character can't benefit from more than one long rest in a 24-hour period.",
                "PHB p.186");

        /// <summary>
        /// Попытка потратить кость хитов, когда их не осталось.
        /// </summary>
        public static RuleViolation NoHitDiceAvailable(Guid characterId, int hitDieType)
            => new(characterId, "HitDice",
                $"No remaining hit dice of type d{hitDieType}.",
                "PHB p.186");

        /// <summary>
        /// Попытка надеть доспех без необходимого навыка.
        /// </summary>
        public static RuleViolation ArmorProficiencyRequired(Guid characterId, string armorName)
            => new(characterId, "ArmorProficiency",
                $"You are not proficient with {armorName}. Disadvantage on attacks, saves, and ability checks.",
                "PHB p.144");

        /// <summary>
        /// Попытка скрытного перемещения в тяжёлом доспехе.
        /// </summary>
        public static RuleViolation StealthDisadvantageHeavyArmor(Guid characterId, string armorName)
            => new(characterId, "Stealth",
                $"You have disadvantage on Dexterity (Stealth) checks while wearing {armorName}.",
                "PHB p.144");

        /// <summary>
        /// Попытка колдовать заклинание без необходимых компонентов (вербальный, соматический, материальный).
        /// </summary>
        public static RuleViolation MissingSpellComponent(Guid characterId, string spellId, string componentType)
            => new(characterId, "SpellComponents",
                $"Cannot cast '{spellId}': missing {componentType} component.",
                "PHB p.203");

        /// <summary>
        /// Превышение максимального уровня персонажа (20).
        /// </summary>
        public static RuleViolation MaximumLevelExceeded(Guid characterId)
            => new(characterId, "LevelCap",
                "Characters cannot exceed level 20.",
                "PHB p.15");

        /// <summary>
        /// Значение характеристики вне допустимого диапазона (1-30).
        /// </summary>
        public static RuleViolation AbilityScoreOutOfRange(Guid characterId, string ability, int score)
            => new(characterId, "AbilityScores",
                $"Ability score for {ability} must be between 1 and 30. Provided: {score}.",
                "PHB p.173");

        /// <summary>
        /// Попытка подготовить больше заклинаний, чем позволяет уровень + модификатор заклинательной характеристики.
        /// </summary>
        public static RuleViolation TooManyPreparedSpells(Guid characterId, int prepared, int allowed)
            => new(characterId, "SpellPreparation",
                $"You can prepare only {allowed} spells, but attempted to prepare {prepared}.",
                "PHB p.114");

        /// <summary>
        /// Персонаж не может совершать реакции, если он удивлён (Surprised).
        /// </summary>
        public static RuleViolation SurprisedNoReaction(Guid characterId)
            => new(characterId, "Surprise",
                "A surprised creature cannot take reactions.",
                "PHB p.189");
    }
}