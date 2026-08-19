// domain/rules/rest_rules.cs
namespace dnd_game.Domain.Rules;

public static class RestRules
{
    // --------------------------------------------------------------------------------------------
    // Восстановление хитов
    // --------------------------------------------------------------------------------------------
    /// <summary>
    /// Хиты, восстанавливаемые при использовании одной кости хитов во время короткого отдыха.
    /// </summary>
    public static int HitPointsPerHitDie(int roll, int constitutionModifier)
    {
        return Math.Max(0, roll + constitutionModifier);
    }

    /// <summary>
    /// Восстановление хитов при длинном отдыхе – полное.
    /// </summary>
    public static int HitPointsAfterLongRest(int maxHitPoints) => maxHitPoints;

    // --------------------------------------------------------------------------------------------
    // Кости хитов
    // --------------------------------------------------------------------------------------------
    /// <summary>
    /// Максимальное количество костей хитов персонажа (обычно равно уровню).
    /// </summary>
    public static int TotalHitDice(int level) => level;

    /// <summary>
    /// Сколько костей хитов восстанавливается после длинного отдыха (половина максимума, минимум 1).
    /// </summary>
    public static int HitDiceRecoveredOnLongRest(int maxHitDice) =>
        Math.Max(1, maxHitDice / 2);

    /// <summary>
    /// Можно ли тратить кости хитов во время короткого отдыха.
    /// </summary>
    public static bool CanSpendHitDice(int remainingHitDice, int currentHitPoints, int maxHitPoints) =>
        remainingHitDice > 0 && currentHitPoints < maxHitPoints;

    // --------------------------------------------------------------------------------------------
    // Ячейки заклинаний
    // --------------------------------------------------------------------------------------------
    /// <summary>
    /// Восстановление ячеек после длинного отдыха (все ячейки).
    /// </summary>
    public static bool LongRestRestoresAllSpellSlots => true;

    /// <summary>
    /// Восстановление ячеек для классов, восстанавливающих ячейки на коротком отдыхе (Warlock).
    /// </summary>
    public static bool ShortRestRestoresPactMagicSlots => true; // для Warlock

    // --------------------------------------------------------------------------------------------
    // Классовые умения и ресурсы
    // --------------------------------------------------------------------------------------------
    public static bool RechargesOnShortRest(string featureId)
    {
        if (string.IsNullOrEmpty(featureId))
        {
            throw new ArgumentException($"\"{nameof(featureId)}\" не может быть неопределенным или пустым.", nameof(featureId));
        }

        return false;   // должно определяться по данным умения
    }

    public static bool RechargesOnLongRest(string featureId)
    {
        if (string.IsNullOrEmpty(featureId))
        {
            throw new ArgumentException($"\"{nameof(featureId)}\" не может быть неопределенным или пустым.", nameof(featureId));
        }

        return true;
    }

    // --------------------------------------------------------------------------------------------
    // Ограничения по времени
    // --------------------------------------------------------------------------------------------
    /// <summary>
    /// Нельзя получить пользу более чем от одного длинного отдыха за 24-часовой период.
    /// </summary>
    public static bool CanBenefitFromLongRest(DateTime lastLongRestEnd) =>
        (DateTime.UtcNow - lastLongRestEnd).TotalHours >= 24;

    /// <summary>
    /// Длительность короткого отдыха – минимум 1 час.
    /// </summary>
    public static int ShortRestMinimumDurationHours => 1;

    /// <summary>
    /// Длительность длинного отдыха – 8 часов (из которых не менее 6 часов сна).
    /// </summary>
    public static int LongRestMinimumDurationHours => 8;

    /// <summary>
    /// Для рас с трансом (эльфы) длительность сна может быть 4 часа, но отдых всё равно 8 часов лёгкой активности.
    /// </summary>
    public static int ElfTranceSleepHours => 4;

    // --------------------------------------------------------------------------------------------
    // Прерывание отдыха
    // --------------------------------------------------------------------------------------------
    /// <summary>
    /// Короткий отдых прерывается боем или другой напряжённой активностью; преимущества теряются.
    /// </summary>
    public static bool ShortRestInterruptedByCombat => true;

    /// <summary>
    /// Длинный отдых прерывается, если период напряжённой активности превышает 1 час (бой, колдовство, ходьба).
    /// Если прерывание меньше часа, отдых можно продолжить.
    /// </summary>
    public static bool LongRestInterruptedByStrenuousActivity(int activityHours) =>
        activityHours > 1;

    // --------------------------------------------------------------------------------------------
    // Сон и усталость
    // --------------------------------------------------------------------------------------------
    /// <summary>
    /// Длинный отдых снимает 1 уровень усталости, если персонаж поел и выпил.
    /// </summary>
    public static int ExhaustionReductionOnLongRest(bool hasEatenAndDrunk) =>
        hasEatenAndDrunk ? 1 : 0;

    // --------------------------------------------------------------------------------------------
    // Сон в доспехах (опциональное правило Xanathar's Guide)
    // --------------------------------------------------------------------------------------------
    /// <summary>
    /// Сон в среднем или тяжёлом доспехе восстанавливает только 1/4 костей хитов и не снимает усталость.
    /// </summary>
    public static bool SleepingInMediumOrHeavyArmorReducesRecovery => true;

    public static int HitDiceRecoveredOnLongRestWhileArmored(int maxHitDice) =>
        Math.Max(1, maxHitDice / 4);

    // --------------------------------------------------------------------------------------------
    // Проверка возможности начать отдых
    // --------------------------------------------------------------------------------------------
    /// <summary>
    /// Нельзя отдыхать, если персонаж в бою или находится в состоянии, не позволяющем отдых (например, умирает).
    /// </summary>
    public static bool CanStartRest(bool isInCombat, bool isDying, bool isUnconscious) =>
        !isInCombat && !isDying && !isUnconscious;
}