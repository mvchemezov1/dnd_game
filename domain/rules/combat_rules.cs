using dnd_game.SharedKernel;
using dnd_game.Domain.ValueObjects;
using System.Collections.Generic;
using System.Linq;

namespace dnd_game.Domain.Rules
{
    public static class CombatRules
    {
        // --------------------------------------------------------------------------------
        // 1. Инициатива
        // --------------------------------------------------------------------------------

        /// <summary>
        /// Вычисляет значение инициативы: d20 + модификатор ловкости + дополнительные бонусы.
        /// </summary>
        public static int CalculateInitiative(int d20Roll, int dexterityModifier, int miscBonus = 0)
        {
            return d20Roll + dexterityModifier + miscBonus;
        }

        // --------------------------------------------------------------------------------
        // 2. Броски атаки
        // --------------------------------------------------------------------------------

        /// <summary>
        /// Вычисляет результат броска атаки: d20 + бонус мастерства + модификатор атакующей характеристики + прочие бонусы.
        /// </summary>
        public static int CalculateAttackRoll(int d20Roll, int proficiencyBonus, int abilityModifier, int miscBonus = 0)
        {
            return d20Roll + proficiencyBonus + abilityModifier + miscBonus;
        }

        /// <summary>
        /// Проверяет, попала ли атака (результат броска >= КД цели).
        /// </summary>
        public static bool IsHit(int attackRoll, int targetArmorClass)
        {
            return attackRoll >= targetArmorClass;
        }

        /// <summary>
        /// Является ли бросок критическим успехом (натуральное 20).
        /// </summary>
        public static bool IsCriticalHit(int d20Roll) => d20Roll == 20;

        /// <summary>
        /// Является ли бросок критическим провалом (натуральное 1).
        /// </summary>
        public static bool IsCriticalMiss(int d20Roll) => d20Roll == 1;

        // --------------------------------------------------------------------------------
        // 3. Урон
        // --------------------------------------------------------------------------------

        /// <summary>
        /// Вычисляет итоговый урон с учётом модификатора (обычно модификатор характеристики для атаки оружием).
        /// </summary>
        public static int CalculateDamage(int baseDamage, int modifier)
        {
            return baseDamage + modifier;
        }

        /// <summary>
        /// Применяет сопротивления, уязвимости и иммунитеты к урону согласно правилам DnD 5e.
        /// </summary>
        /// <param name="incomingDamage">Исходный урон после модификаторов.</param>
        /// <param name="damageType">Тип урона (строка, соответствует перечислению DamageType).</param>
        /// <param name="resistances">Список типов урона, к которым есть сопротивление.</param>
        /// <param name="vulnerabilities">Список типов урона, к которым есть уязвимость.</param>
        /// <param name="immunities">Список типов урона, к которым есть иммунитет.</param>
        /// <returns>Итоговый урон после модификаций (всегда >= 0).</returns>
        public static int ApplyDamageModifiers(
            int incomingDamage,
            string damageType,
            IEnumerable<string> resistances,
            IEnumerable<string> vulnerabilities,
            IEnumerable<string> immunities)
        {
            if (immunities.Contains(damageType, StringComparer.OrdinalIgnoreCase))
                return 0;

            int finalDamage = incomingDamage;

            if (vulnerabilities.Contains(damageType, StringComparer.OrdinalIgnoreCase))
                finalDamage *= 2;

            if (resistances.Contains(damageType, StringComparer.OrdinalIgnoreCase))
                finalDamage /= 2;

            return Math.Max(0, finalDamage);
        }

        // --------------------------------------------------------------------------------
        // 4. Преимущество и помеха (Advantage / Disadvantage)
        // --------------------------------------------------------------------------------

        /// <summary>
        /// Выполняет бросок с преимуществом (выбирается наибольший из двух d20).
        /// </summary>
        public static int RollWithAdvantage(int roll1, int roll2) => Math.Max(roll1, roll2);

        /// <summary>
        /// Выполняет бросок с помехой (выбирается наименьший из двух d20).
        /// </summary>
        public static int RollWithDisadvantage(int roll1, int roll2) => Math.Min(roll1, roll2);

        // --------------------------------------------------------------------------------
        // 5. Дополнительные проверки (концентрация, спасброски)
        // --------------------------------------------------------------------------------

        /// <summary>
        /// Сложность проверки концентрации: DC = max(10, damage_taken / 2).
        /// </summary>
        public static int CalculateConcentrationDC(int damageTaken) => Math.Max(10, damageTaken / 2);

        /// <summary>
        /// Проверяет, успешен ли спасбросок.
        /// </summary>
        public static bool IsSavingThrowSuccess(int d20Roll, int abilityModifier, int proficiencyBonus, bool isProficient, int difficultyClass)
        {
            int total = d20Roll + abilityModifier + (isProficient ? proficiencyBonus : 0);
            return total >= difficultyClass;
        }

        // --------------------------------------------------------------------------------
        // 6. Вспомогательные методы (для типов урона и оружия)
        // --------------------------------------------------------------------------------

        /// <summary>
        /// Возвращает средний урон для заданного количества костей и их граней.
        /// </summary>
        public static int AverageDamage(int numberOfDice, int diceSides)
        {
            return numberOfDice * (diceSides + 1) / 2;
        }

        /// <summary>
        /// Возвращает средний урон для выражения вида "NdX" (например, "2d6").
        /// </summary>
        public static int AverageDamage(string diceNotation)
        {
            var parts = diceNotation.ToLower().Split('d');
            if (parts.Length != 2) throw new ArgumentException("Invalid dice notation. Expected format: NdX");
            if (!int.TryParse(parts[0], out int count)) throw new ArgumentException("Invalid dice count");
            if (!int.TryParse(parts[1], out int sides)) throw new ArgumentException("Invalid dice sides");
            return AverageDamage(count, sides);
        }

        // --------------------------------------------------------------------------------
        // 7. Проверка дистанции для атак
        // --------------------------------------------------------------------------------

        /// <summary>
        /// Проверяет, находится ли цель в пределах дальности атаки (в футах).
        /// </summary>
        public static bool IsInRange(int distanceFeet, int weaponRangeFeet) => distanceFeet <= weaponRangeFeet;

        /// <summary>
        /// Проверяет, находится ли цель в пределах дальности для атаки с помехой (длинный диапазон).
        /// </summary>
        public static bool IsInLongRange(int distanceFeet, int longRangeFeet) => distanceFeet <= longRangeFeet;
    }
}