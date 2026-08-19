// domain/value_objects/currency.cs
namespace dnd_game.domain.value_objects
{
    /// <summary>
    /// Представляет количество денег в мире DnD.
    /// Хранит общую сумму в медных монетах (cp) и позволяет получать нормализованное количество
    /// монет каждого достоинства: платина (pp), золото (gp), электрум (ep), серебро (sp), медь (cp).
    /// Курсы обмена (5e): 1 pp = 10 gp, 1 gp = 10 sp, 1 ep = 5 sp, 1 sp = 10 cp.
    /// </summary>
    public record Currency : IComparable<Currency>
    {
        // ---------- Константы обмена ----------
        public const int CopperPerSilver = 10;
        public const int CopperPerElectrum = 50;   // 1 ep = 5 sp = 50 cp
        public const int CopperPerGold = 100;       // 1 gp = 10 sp = 100 cp
        public const int CopperPerPlatinum = 1000;  // 1 pp = 10 gp = 1000 cp

        /// <summary>Общее количество медных монет (базовая единица).</summary>
        public int TotalCopper { get; }

        // ---------- Вычисляемые нормализованные компоненты ----------
        public int Platinum => TotalCopper / CopperPerPlatinum;
        public int Gold => TotalCopper % CopperPerPlatinum / CopperPerGold;
        public int Electrum => TotalCopper % CopperPerGold / CopperPerElectrum;
        public int Silver => TotalCopper % CopperPerElectrum / CopperPerSilver;
        public int Copper => TotalCopper % CopperPerSilver;

        // ---------- Конструкторы ----------

        /// <summary>
        /// Создаёт валюту из общего количества медных монет.
        /// </summary>
        /// <param name="totalCopper">Неотрицательное количество медных монет.</param>
        /// <exception cref="ArgumentException">Если сумма отрицательна.</exception>
        public Currency(int totalCopper)
        {
            if (totalCopper < 0)
                throw new ArgumentException("Currency amount cannot be negative.", nameof(totalCopper));
            TotalCopper = totalCopper;
        }

        // ---------- Статические фабричные методы ----------

        /// <summary>Создаёт валюту только из медных монет.</summary>
        public static Currency FromCopper(int copper) => new(copper);

        /// <summary>Создаёт валюту из серебряных монет (конвертирует в медь).</summary>
        public static Currency FromSilver(int silver) => new(silver * CopperPerSilver);

        /// <summary>Создаёт валюту из электрумовых монет.</summary>
        public static Currency FromElectrum(int electrum) => new(electrum * CopperPerElectrum);

        /// <summary>Создаёт валюту из золотых монет.</summary>
        public static Currency FromGold(int gold) => new(gold * CopperPerGold);

        /// <summary>Создаёт валюту из платиновых монет.</summary>
        public static Currency FromPlatinum(int platinum) => new(platinum * CopperPerPlatinum);

        /// <summary>
        /// Создаёт валюту, задавая точное количество монет каждого типа.
        /// Все параметры должны быть неотрицательными.
        /// </summary>
        public static Currency FromComponents(int platinum, int gold, int electrum, int silver, int copper)
        {
            if (platinum < 0 || gold < 0 || electrum < 0 || silver < 0 || copper < 0)
                throw new ArgumentException("All currency amounts must be non-negative.");
            int total = platinum * CopperPerPlatinum
                      + gold * CopperPerGold
                      + electrum * CopperPerElectrum
                      + silver * CopperPerSilver
                      + copper;
            return new Currency(total);
        }

        /// <summary>Пустая валюта (ноль).</summary>
        public static Currency Zero => new(0);

        // ---------- Операторы ----------

        public static Currency operator +(Currency a, Currency b) => new(a.TotalCopper + b.TotalCopper);
        public static Currency operator -(Currency a, Currency b)
        {
            int result = a.TotalCopper - b.TotalCopper;
            if (result < 0)
                throw new InvalidOperationException("Cannot subtract a larger currency amount from a smaller one (result would be negative).");
            return new Currency(result);
        }
        public static Currency operator *(Currency a, int multiplier)
        {
            if (multiplier < 0) throw new ArgumentException("Multiplier must be non-negative.");
            return new Currency(a.TotalCopper * multiplier);
        }
        public static bool operator >(Currency a, Currency b) => a.TotalCopper > b.TotalCopper;
        public static bool operator <(Currency a, Currency b) => a.TotalCopper < b.TotalCopper;
        public static bool operator >=(Currency a, Currency b) => a.TotalCopper >= b.TotalCopper;
        public static bool operator <=(Currency a, Currency b) => a.TotalCopper <= b.TotalCopper;

        // ---------- Методы проверки ----------

        /// <summary>Проверяет, достаточно ли средств для оплаты указанной стоимости.</summary>
        public bool CanAfford(Currency cost) => TotalCopper >= cost.TotalCopper;

        /// <summary>
        /// Вычитает указанную стоимость и возвращает оставшуюся сумму.
        /// Бросает исключение, если средств недостаточно.
        /// </summary>
        public Currency Subtract(Currency cost)
        {
            if (!CanAfford(cost))
                throw new InvalidOperationException("Insufficient funds.");
            return new Currency(TotalCopper - cost.TotalCopper);
        }

        /// <summary>Добавляет другую валюту и возвращает новую сумму.</summary>
        public Currency Add(Currency other) => new(TotalCopper + other.TotalCopper);

        // ---------- Сравнение ----------
        public int CompareTo(Currency? other) => TotalCopper.CompareTo(other?.TotalCopper ?? 0);

        // ---------- Вспомогательные методы ----------

        /// <summary>
        /// Возвращает нормализованное представление валюты в виде словаря (тип монеты -> количество).
        /// </summary>
        public Dictionary<string, int> Breakdown() => new()
        {
            ["pp"] = Platinum,
            ["gp"] = Gold,
            ["ep"] = Electrum,
            ["sp"] = Silver,
            ["cp"] = Copper
        };

        public override string ToString()
        {
            var parts = new List<string>();
            if (Platinum > 0) parts.Add($"{Platinum} pp");
            if (Gold > 0) parts.Add($"{Gold} gp");
            if (Electrum > 0) parts.Add($"{Electrum} ep");
            if (Silver > 0) parts.Add($"{Silver} sp");
            if (Copper > 0 || parts.Count == 0) parts.Add($"{Copper} cp");
            return string.Join(", ", parts);
        }
    }
}