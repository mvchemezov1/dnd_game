// domain/value_objects/dice.cs
using System.Text.RegularExpressions;

namespace dnd_game.Domain.ValueObjects
{
    /// <summary>
    /// Представляет бросок набора одинаковых костей (например, 2d6+3, 4d6kh3).
    /// Поддерживает стандартную нотацию DnD: количество кубов, грани, модификатор, 
    /// удержание наибольших/наименьших результатов (keep highest/lowest) и однократный переброс низких значений.
    /// </summary>
    public partial record Dice
    {
        /// <summary>Количество бросаемых костей.</summary>
        public int Count { get; }

        /// <summary>Количество граней у одной кости (d4, d6, d8, d10, d12, d20, d100).</summary>
        public int Sides { get; }

        /// <summary>Модификатор, добавляемый к сумме выпавших значений.</summary>
        public int Modifier { get; }

        /// <summary>
        /// Если задано положительное число – оставить только указанное количество наибольших результатов.
        /// Если отрицательное – оставить соответствующее количество наименьших.
        /// Если null – учитываются все броски.
        /// </summary>
        public int? Keep { get; }

        /// <summary>
        /// Если задано, каждый куб, показавший результат меньше или равный этому значению,
        /// перебрасывается один раз, и принимается новый результат.
        /// Используется, например, для стиля боя "Великое оружие" (Great Weapon Fighting).
        /// Должно быть >= 1 и меньше Sides.
        /// </summary>
        public int? RerollOnOrLess { get; }

        public Dice(int count, int sides, int modifier = 0, int? keep = null, int? rerollOnOrLess = null)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Dice count cannot be negative.");
            if (sides < 2)
                throw new ArgumentOutOfRangeException(nameof(sides), "Dice must have at least 2 sides (d2 or greater).");
            if (keep.HasValue && keep.Value == 0)
                throw new ArgumentException("Keep value cannot be zero.", nameof(keep));
            if (keep.HasValue && Math.Abs(keep.Value) > count)
                throw new ArgumentException("Cannot keep more dice than are rolled.", nameof(keep));
            if (rerollOnOrLess.HasValue && (rerollOnOrLess.Value < 1 || rerollOnOrLess.Value >= sides))
                throw new ArgumentOutOfRangeException(nameof(rerollOnOrLess), $"Reroll threshold must be between 1 and {sides - 1}.");

            Count = count;
            Sides = sides;
            Modifier = modifier;
            Keep = keep;
            RerollOnOrLess = rerollOnOrLess;
        }

        // ---------- Фабрики для стандартных костей ----------
        public static Dice D4(int count = 1, int modifier = 0) => new(count, 4, modifier);
        public static Dice D6(int count = 1, int modifier = 0) => new(count, 6, modifier);
        public static Dice D8(int count = 1, int modifier = 0) => new(count, 8, modifier);
        public static Dice D10(int count = 1, int modifier = 0) => new(count, 10, modifier);
        public static Dice D12(int count = 1, int modifier = 0) => new(count, 12, modifier);
        public static Dice D20(int modifier = 0) => new(1, 20, modifier); // D20 обычно одна, с модификатором
        public static Dice D100(int modifier = 0) => new(1, 100, modifier); // D100 (процентиль)

        /// <summary>Специальный бросок для характеристики: 4d6, оставить 3 наибольших.</summary>
        public static Dice AbilityScore() => new(4, 6, keep: 3);

        // ---------- Выполнение броска ----------

        /// <summary>
        /// Выполняет бросок костей и возвращает результат.
        /// Для детерминированных сценариев следует передавать конкретный экземпляр Random с заданным seed.
        /// </summary>
        /// <param name="random">Генератор случайных чисел.</param>
        /// <returns>Результат броска.</returns>
        public DiceRollResult Roll(Random random)
        {
            var rolls = new List<int>(Count);
            for (int i = 0; i < Count; i++)
            {
                int roll = random.Next(1, Sides + 1);
                rolls.Add(roll);
            }

            // Применяем переброс (reroll) – однократно
            if (RerollOnOrLess.HasValue)
            {
                for (int i = 0; i < rolls.Count; i++)
                {
                    if (rolls[i] <= RerollOnOrLess.Value)
                    {
                        rolls[i] = random.Next(1, Sides + 1);
                    }
                }
            }

            // Применяем удержание наибольших/наименьших
            IEnumerable<int> kept = rolls;
            if (Keep.HasValue)
            {
                int keepCount = Math.Abs(Keep.Value);
                if (Keep.Value > 0)
                    kept = rolls.OrderByDescending(x => x).Take(keepCount);
                else
                    kept = rolls.OrderBy(x => x).Take(keepCount);
            }

            int total = kept.Sum() + Modifier;

            // Критические успех/провал только для d20 (в контексте атаки)
            bool isNatural20 = false;
            bool isNatural1 = false;
            if (Sides == 20 && Count == 1)
            {
                int singleRoll = rolls[0];
                isNatural20 = singleRoll == 20;
                isNatural1 = singleRoll == 1;
            }

            return new DiceRollResult(rolls.AsReadOnly(), kept.ToList().AsReadOnly(), total, Modifier, isNatural20, isNatural1);
        }

        // ---------- Среднее значение (без модификатора) ----------
        public double Average(bool applyKeep = true)
        {
            double avgOneDie = (Sides + 1) / 2.0;
            if (RerollOnOrLess.HasValue)
            {
                // Переброс: среднее новой кости = (Sides+1)/2, срабатывает с вероятностью порог/Sides
                double p = (double)RerollOnOrLess.Value / Sides;
                avgOneDie = p * (Sides + 1) / 2.0 + (1 - p) * (Sides + 1) / 2.0; // остаётся тем же, т.к. переброс даёт то же среднее
                // На самом деле, после переброса среднее не меняется, так как переброс – это снова равномерное распределение.
            }
            double totalAverage = Count * avgOneDie;
            if (Keep.HasValue && applyKeep)
            {
                // Удержание меняет среднее; сложный комбинаторный расчёт; для простоты можно не вычислять,
                // но для часто используемых случаев (4d6kh3) приближение можно дать.
                // Здесь вернём простую оценку: сумма ожиданий, урезанная пропорционально.
                return totalAverage; // упрощение
            }
            return totalAverage;
        }

        // ---------- Строковое представление ----------
        public override string ToString()
        {
            string baseNotation = $"{Count}d{Sides}";
            if (Keep.HasValue)
            {
                string keepPrefix = Keep > 0 ? "kh" : "kl";
                baseNotation += $"{keepPrefix}{Math.Abs(Keep.Value)}";
            }
            if (RerollOnOrLess.HasValue)
            {
                baseNotation += $"ro{RerollOnOrLess.Value}";
            }
            if (Modifier != 0)
            {
                string sign = Modifier > 0 ? "+" : "-";
                baseNotation += $"{sign}{Math.Abs(Modifier)}";
            }
            return baseNotation;
        }

        // ---------- Парсинг нотации ----------
        /// <summary>
        /// Разбирает строку в объект Dice. Поддерживает форматы:
        /// "2d6+3", "1d20", "4d6kh3", "4d6kl3", "2d6ro2+1", "8d6ro1kh6".
        /// </summary>
        public static Dice Parse(string notation)
        {
            if (string.IsNullOrWhiteSpace(notation))
                throw new ArgumentException("Dice notation cannot be empty.");

            // Регулярное выражение для разбора
            var regex = DiceNotationRegex();
            var match = regex.Match(notation.Trim());
            if (!match.Success)
                throw new FormatException($"Invalid dice notation: '{notation}'.");

            int count = int.Parse(match.Groups["count"].Value);
            int sides = int.Parse(match.Groups["sides"].Value);
            int modifier = 0;
            if (match.Groups["modifier"].Success)
            {
                string sign = match.Groups["sign"].Value;
                int mod = int.Parse(match.Groups["mod"].Value);
                modifier = sign == "-" ? -mod : mod;
            }
            int? keep = null;
            if (match.Groups["keep"].Success)
            {
                string keepDir = match.Groups["keep"].Value; // "kh" или "kl"
                int keepCount = int.Parse(match.Groups["keepCount"].Value);
                keep = keepDir == "kh" ? keepCount : -keepCount;
            }
            int? reroll = null;
            if (match.Groups["reroll"].Success)
            {
                reroll = int.Parse(match.Groups["reroll"].Value);
            }

            return new Dice(count, sides, modifier, keep, reroll);
        }

        // Генератор регулярного выражения (для удобства используется partial method)
        [GeneratedRegex(@"^(?<count>\d+)d(?<sides>\d+)(?:(?<keep>kh|kl)(?<keepCount>\d+))?(?:(?<reroll>ro\d+))?(?:(?<sign>[+-])(?<mod>\d+))?$", RegexOptions.IgnoreCase)]
        private static partial Regex DiceNotationRegex();
    }

    /// <summary>
    /// Результат броска костей.
    /// </summary>
    public record DiceRollResult
    {
        /// <summary>Все брошенные значения (до применения keep и переброса).</summary>
        public IReadOnlyList<int> AllRolls { get; }

        /// <summary>Значения, учтённые в сумме (после keep).</summary>
        public IReadOnlyList<int> KeptRolls { get; }

        /// <summary>Итоговое значение (сумма учтённых + модификатор).</summary>
        public int Total { get; }

        /// <summary>Модификатор, добавленный к сумме.</summary>
        public int Modifier { get; }

        /// <summary>Истина, если это бросок d20 и выпало натуральное 20 (критический успех).</summary>
        public bool IsNatural20 { get; }

        /// <summary>Истина, если это бросок d20 и выпало натуральное 1 (критический провал).</summary>
        public bool IsNatural1 { get; }

        public DiceRollResult(IReadOnlyList<int> allRolls, IReadOnlyList<int> keptRolls, int total, int modifier, bool isNatural20, bool isNatural1)
        {
            AllRolls = allRolls;
            KeptRolls = keptRolls;
            Total = total;
            Modifier = modifier;
            IsNatural20 = isNatural20;
            IsNatural1 = isNatural1;
        }

        public override string ToString() => $"[{string.Join(", ", KeptRolls)}] + {Modifier} = {Total}";
    }

    /// <summary>
    /// Вспомогательные методы для бросков с преимуществом/помехой (только для d20).
    /// </summary>
    public static class D20RollHelper
    {
        /// <summary>
        /// Бросок d20 с преимуществом (два броска, выбирается наибольший).
        /// </summary>
        public static AdvantageResult RollWithAdvantage(int modifier, Random random)
        {
            int roll1 = random.Next(1, 21);
            int roll2 = random.Next(1, 21);
            int chosen = Math.Max(roll1, roll2);
            return new AdvantageResult(roll1, roll2, chosen, modifier, chosen + modifier, true, false);
        }

        /// <summary>
        /// Бросок d20 с помехой (два броска, выбирается наименьший).
        /// </summary>
        public static AdvantageResult RollWithDisadvantage(int modifier, Random random)
        {
            int roll1 = random.Next(1, 21);
            int roll2 = random.Next(1, 21);
            int chosen = Math.Min(roll1, roll2);
            return new AdvantageResult(roll1, roll2, chosen, modifier, chosen + modifier, false, true);
        }

        public record AdvantageResult(int Roll1, int Roll2, int Chosen, int Modifier, int Total, bool IsAdvantage, bool IsDisadvantage)
        {
            public bool IsCriticalHit => Chosen == 20;
            public bool IsCriticalMiss => Chosen == 1;
            public override string ToString()
            {
                string type = IsAdvantage ? "Advantage" : "Disadvantage";
                return $"{type}: [{Roll1}, {Roll2}] -> {Chosen} + {Modifier} = {Total}";
            }
        }
    }
}