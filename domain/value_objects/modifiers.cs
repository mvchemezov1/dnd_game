// domain/value_objects/modifiers.cs

// domain/value_objects/modifiers.cs
namespace dnd_game.domain.value_objects
{
    /// <summary>
    /// Значения шести характеристик персонажа.
    /// </summary>
    public record AbilityScores(int Strength, int Dexterity, int Constitution,
                                int Intelligence, int Wisdom, int Charisma)
    {
        public static readonly AbilityScores Default = new(10, 10, 10, 10, 10, 10);

        /// <summary>
        /// Возвращает модификатор характеристики (бонус/штраф) для указанной способности.
        /// </summary>
        public int GetModifier(AbilityId ability) => ability.Value switch
        {
            "Strength" => ModifierCalculator.Calculate(Strength),
            "Dexterity" => ModifierCalculator.Calculate(Dexterity),
            "Constitution" => ModifierCalculator.Calculate(Constitution),
            "Intelligence" => ModifierCalculator.Calculate(Intelligence),
            "Wisdom" => ModifierCalculator.Calculate(Wisdom),
            "Charisma" => ModifierCalculator.Calculate(Charisma),
            _ => throw new ArgumentOutOfRangeException(nameof(ability), $"Unknown ability: {ability.Value}")
        };

        /// <summary>
        /// Устанавливает новое значение одной характеристики, возвращая обновлённый объект.
        /// </summary>
        public AbilityScores With(AbilityId ability, int score) => ability.Value switch
        {
            "Strength" => this with { Strength = score },
            "Dexterity" => this with { Dexterity = score },
            "Constitution" => this with { Constitution = score },
            "Intelligence" => this with { Intelligence = score },
            "Wisdom" => this with { Wisdom = score },
            "Charisma" => this with { Charisma = score },
            _ => throw new ArgumentOutOfRangeException(nameof(ability))
        };
    }

    /// <summary>
    /// Модификаторы характеристик (уже вычисленные значения).
    /// </summary>
    public record AbilityModifiers(int Strength, int Dexterity, int Constitution,
                                   int Intelligence, int Wisdom, int Charisma)
    {
        public static AbilityModifiers FromScores(AbilityScores scores) => new(
            ModifierCalculator.Calculate(scores.Strength),
            ModifierCalculator.Calculate(scores.Dexterity),
            ModifierCalculator.Calculate(scores.Constitution),
            ModifierCalculator.Calculate(scores.Intelligence),
            ModifierCalculator.Calculate(scores.Wisdom),
            ModifierCalculator.Calculate(scores.Charisma)
        );

        /// <summary>
        /// Возвращает модификатор для конкретной характеристики.
        /// </summary>
        public int Get(AbilityId ability) => ability.Value switch
        {
            "Strength" => Strength,
            "Dexterity" => Dexterity,
            "Constitution" => Constitution,
            "Intelligence" => Intelligence,
            "Wisdom" => Wisdom,
            "Charisma" => Charisma,
            _ => throw new ArgumentOutOfRangeException(nameof(ability))
        };
    }

    /// <summary>
    /// Бонус мастерства (Proficiency Bonus).
    /// </summary>
    public record ProficiencyBonus(int Value)
    {
        public static ProficiencyBonus FromLevel(int level) => level switch
        {
            <= 4 => new(2),
            <= 8 => new(3),
            <= 12 => new(4),
            <= 16 => new(5),
            _ => new(6)
        };

        public override string ToString() => $"+{Value}";
    }

    /// <summary>
    /// Модификаторы для различных бросков: атаки, урона, спасбросков, навыков, КД.
    /// </summary>
    public record CombatModifiers(
        int AttackBonus = 0,
        int DamageBonus = 0,
        int ArmorClassBonus = 0,
        int SavingThrowBonus = 0,
        int SpellAttackBonus = 0,
        int SpellSaveDCBonus = 0)
    {
        public static readonly CombatModifiers Zero = new();
    }

    /// <summary>
    /// Составной модификатор проверки навыка: учитывает бонус мастерства, модификатор характеристики,
    /// дополнительные бонусы, экспертизу (double proficiency) и помеху/преимущество.
    /// </summary>
    public record SkillCheckModifier(int AbilityModifier, int ProficiencyBonus, bool IsProficient,
                                     bool Expertise = false, int MiscBonus = 0)
    {
        public int TotalBonus => AbilityModifier + (IsProficient ? Expertise ? 2 * ProficiencyBonus : ProficiencyBonus : 0) + MiscBonus;
    }

    /// <summary>
    /// Модификаторы для спасброска.
    /// </summary>
    public record SavingThrowModifier(int AbilityModifier, int ProficiencyBonus, bool IsProficient,
                                      int MiscBonus = 0)
    {
        public int TotalBonus => AbilityModifier + (IsProficient ? ProficiencyBonus : 0) + MiscBonus;
    }

    /// <summary>
    /// Вспомогательный класс для вычисления модификаторов.
    /// </summary>
    public static class ModifierCalculator
    {
        /// <summary>
        /// Вычисляет модификатор характеристики DnD 5e: (score - 10) / 2 с округлением вниз.
        /// </summary>
        public static int Calculate(int abilityScore) => (abilityScore - 10) / 2;

        /// <summary>
        /// Пассивная проверка навыка: 10 + модификатор навыка + (преимущество +5, помеха -5).
        /// </summary>
        public static int PassiveSkill(int baseValue, bool hasAdvantage = false, bool hasDisadvantage = false)
        {
            int result = baseValue;
            if (hasAdvantage) result += 5;
            if (hasDisadvantage) result -= 5;
            return result;
        }

        /// <summary>
        /// Инициатива: бросок d20 + модификатор Ловкости + бонусы.
        /// </summary>
        public static int InitiativeModifier(int dexterityModifier, int miscBonus = 0) =>
            dexterityModifier + miscBonus;

        /// <summary>
        /// Класс Брони (AC) для лёгкого доспеха: база + модификатор Ловкости (возможно с ограничением).
        /// </summary>
        public static int LightArmorAC(int baseArmor, int dexterityModifier, int? maxDexBonus = null) =>
            baseArmor + (maxDexBonus.HasValue ? Math.Min(dexterityModifier, maxDexBonus.Value) : dexterityModifier);

        /// <summary>
        /// Класс Брони для среднего доспеха: база + модификатор Ловкости (макс. 2).
        /// </summary>
        public static int MediumArmorAC(int baseArmor, int dexterityModifier) =>
            baseArmor + Math.Min(dexterityModifier, 2);

        /// <summary>
        /// Класс Брони для тяжёлого доспеха: база (модификатор Ловкости не применяется).
        /// </summary>
        public static int HeavyArmorAC(int baseArmor) => baseArmor;

        /// <summary>
        /// Класс Брони без доспеха (Unarmored Defense): 10 + модификатор Ловкости + возможно другие модификаторы.
        /// </summary>
        public static int UnarmoredAC(int dexterityModifier, int? additionalModifier = null) =>
            10 + dexterityModifier + (additionalModifier ?? 0);
    }
}