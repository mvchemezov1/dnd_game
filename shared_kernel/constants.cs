// shared_kernel/constants.cs
namespace dnd_game.SharedKernel
{
    /// <summary>
    /// Основные константы DnD, используемые всей системой.
    /// </summary>
    public static class GameConstants
    {
        // ────────────────────────────────────────────────────────────
        // Персонажи и группа
        // ────────────────────────────────────────────────────────────
        public const int MaxPartySize = 5;
        public const int MinLevel = 1;
        public const int MaxLevel = 20;
        public const int MinAbilityScore = 1;
        public const int MaxAbilityScore = 30;
        public const int MaxCharacterNameLength = 50;
        public const int MaxConditionsPerCharacter = 20;
        public const int MaxInventorySlots = 500;

        // ────────────────────────────────────────────────────────────
        // Сетка и перемещение
        // ────────────────────────────────────────────────────────────
        public const int DefaultGridSize = 20;                  // количество клеток
        public const int DefaultGridCellSizeFeet = 5;           // футов в клетке
        public const int DefaultSpeedFeet = 30;                 // базовая скорость в футах
        public const int MaxFlightSpeedFeet = 120;
        public const int MaxBurrowSpeedFeet = 30;

        // ────────────────────────────────────────────────────────────
        // Бой
        // ────────────────────────────────────────────────────────────
        public const int MaxParticipantsPerCombat = 50;
        public const int SurpriseRounds = 1;
        public const int DeathSaveSuccessesToStabilize = 3;
        public const int DeathSaveFailuresToDie = 3;
        public const int CriticalHitRoll = 20;
        public const int CriticalMissRoll = 1;
        public const int MaxFallDamageDice = 20;                // 20d6

        // ────────────────────────────────────────────────────────────
        // Отдых
        // ────────────────────────────────────────────────────────────
        public const int ShortRestDurationHours = 1;
        public const int LongRestDurationHours = 8;
        public const int LongRestCooldownHours = 24;
        public const int ElfTranceHours = 4;
        public const int MaxLongRestInterruptionMinutes = 60;

        // ────────────────────────────────────────────────────────────
        // Магия
        // ────────────────────────────────────────────────────────────
        public const int MaxSpellLevel = 9;
        public const int MaxKnownSpells = 300;
        public const int MaxAttunedItems = 3;
        public const int MaxConcentrationSpells = 1;

        // ────────────────────────────────────────────────────────────
        // Восприятие и видение
        // ────────────────────────────────────────────────────────────
        public const int DefaultDarkvisionRangeFeet = 60;
        public const int DefaultBlindsightRangeFeet = 30;
        public const int DefaultTremorsenseRangeFeet = 60;
        public const int DefaultTruesightRangeFeet = 120;
        public const int DimLightPerceptionPenalty = 5;
        public const int MaxVisualRangeFeet = 1200;            // ясная погода

        // ────────────────────────────────────────────────────────────
        // Валюта
        // ────────────────────────────────────────────────────────────
        public const int CopperPerSilver = 10;
        public const int CopperPerElectrum = 50;
        public const int CopperPerGold = 100;
        public const int CopperPerPlatinum = 1000;

        // ────────────────────────────────────────────────────────────
        // Опыт и уровни (таблица PHB)
        // ────────────────────────────────────────────────────────────
        public static readonly Dictionary<int, int> ExperienceThresholds = new()
        {
            { 1, 0 },       { 2, 300 },     { 3, 900 },     { 4, 2700 },
            { 5, 6500 },    { 6, 14000 },   { 7, 23000 },   { 8, 34000 },
            { 9, 48000 },   { 10, 64000 },  { 11, 85000 },  { 12, 100000 },
            { 13, 120000 }, { 14, 140000 }, { 15, 165000 }, { 16, 195000 },
            { 17, 225000 }, { 18, 265000 }, { 19, 305000 }, { 20, 355000 }
        };
    }
}