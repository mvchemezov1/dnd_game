// shared_kernel/utils.cs
namespace dnd_game.SharedKernel
{
    /// <summary>
    /// Вспомогательные утилиты, используемые во всех слоях приложения DnD.
    /// </summary>
    public static class Utils
    {
        private static readonly Random SharedRandom = Random.Shared;

        // --------------------------------------------------------------------------------
        // Броски костей
        // --------------------------------------------------------------------------------
        public static int RollD20() => SharedRandom.Next(1, 21);
        public static int RollD12() => SharedRandom.Next(1, 13);
        public static int RollD10() => SharedRandom.Next(1, 11);
        public static int RollD8() => SharedRandom.Next(1, 9);
        public static int RollD6() => SharedRandom.Next(1, 7);
        public static int RollD4() => SharedRandom.Next(1, 5);
        public static int RollD100() => SharedRandom.Next(1, 101);

        /// <summary>
        /// Бросок заданного количества одинаковых костей.
        /// </summary>
        public static int RollDice(int count, int sides, int modifier = 0)
        {
            int total = 0;
            for (int i = 0; i < count; i++)
                total += SharedRandom.Next(1, sides + 1);
            return total + modifier;
        }

        /// <summary>
        /// Бросок с преимуществом – два d20, выбирается наибольший.
        /// </summary>
        public static int RollWithAdvantage()
        {
            int roll1 = RollD20();
            int roll2 = RollD20();
            return Math.Max(roll1, roll2);
        }

        /// <summary>
        /// Бросок с помехой – два d20, выбирается наименьший.
        /// </summary>
        public static int RollWithDisadvantage()
        {
            int roll1 = RollD20();
            int roll2 = RollD20();
            return Math.Min(roll1, roll2);
        }

        // --------------------------------------------------------------------------------
        // Характеристики и модификаторы
        // --------------------------------------------------------------------------------
        /// <summary>
        /// Вычисление модификатора характеристики по правилам DnD 5e: (score - 10) / 2 с округлением вниз.
        /// </summary>
        public static int AbilityModifier(int abilityScore) => (abilityScore - 10) / 2;

        /// <summary>
        /// Вычисление пассивной проверки навыка: 10 + модификатор навыка.
        /// </summary>
        public static int PassiveCheck(int abilityModifier, int proficiencyBonus, bool isProficient = false,
                                       bool hasAdvantage = false, bool hasDisadvantage = false)
        {
            int baseValue = 10 + abilityModifier + (isProficient ? proficiencyBonus : 0);
            if (hasAdvantage) baseValue += 5;
            if (hasDisadvantage) baseValue -= 5;
            return baseValue;
        }

        // --------------------------------------------------------------------------------
        // Проверки успеха
        // --------------------------------------------------------------------------------
        /// <summary>
        /// Проверяет, успешен ли бросок атаки против указанного КД.
        /// </summary>
        public static bool IsAttackHit(int attackRoll, int armorClass) => attackRoll >= armorClass;

        /// <summary>
        /// Проверяет, успешен ли бросок спасброска или проверки навыка против DC.
        /// </summary>
        public static bool IsCheckSuccessful(int roll, int modifier, int difficultyClass) =>
            (roll + modifier) >= difficultyClass;

        // --------------------------------------------------------------------------------
        // Расстояния и клетки
        // --------------------------------------------------------------------------------
        /// <summary>
        /// Преобразует футы в количество клеток (по умолчанию 5 футов = 1 клетка).
        /// </summary>
        public static int FeetToSquares(int feet, int squareSizeFeet = 5) => feet / squareSizeFeet;

        /// <summary>
        /// Преобразует клетки в футы.
        /// </summary>
        public static int SquaresToFeet(int squares, int squareSizeFeet = 5) => squares * squareSizeFeet;

        // --------------------------------------------------------------------------------
        // Работа со строками
        // --------------------------------------------------------------------------------
        /// <summary>
        /// Проверяет, является ли строка допустимым непустым значением.
        /// </summary>
        public static bool IsNonEmpty(string? value) => !string.IsNullOrWhiteSpace(value);

        /// <summary>
        /// Усекает строку до указанной длины, добавляя многоточие при необходимости.
        /// </summary>
        public static string Truncate(string value, int maxLength) =>
            value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";

        /// <summary>
        /// Генерирует безопасное имя файла/ключа из произвольной строки.
        /// </summary>
        public static string SanitizeName(string name) =>
            string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim('_');

        // --------------------------------------------------------------------------------
        // Случайные выборки (таблицы)
        // --------------------------------------------------------------------------------
        /// <summary>
        /// Выбирает случайный элемент из списка.
        /// </summary>
        public static T RandomElement<T>(IList<T> list)
        {
            if (list == null || list.Count == 0)
                throw new ArgumentException("List must not be null or empty.");
            return list[SharedRandom.Next(list.Count)];
        }

        /// <summary>
        /// Генерирует случайный цвет (Hex).
        /// </summary>
        public static string RandomHexColor()
        {
            return $"#{SharedRandom.Next(0x1000000):X6}";
        }

        // --------------------------------------------------------------------------------
        // Прочие полезные методы
        // --------------------------------------------------------------------------------
        /// <summary>
        /// Ограничивает целое число заданным диапазоном.
        /// </summary>
        public static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        /// <summary>
        /// Округляет значение с плавающей запятой до ближайшего целого вниз (для урона и т.п.).
        /// </summary>
        public static int Floor(double value) => (int)Math.Floor(value);

        /// <summary>
        /// Возвращает текущую временную метку в формате UTC.
        /// </summary>
        public static DateTime UtcNow() => DateTime.UtcNow;
    }
}