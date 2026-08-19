// application/projections/character_dto.cs
namespace dnd_game.Application.Projections;

/// <summary>
/// DTO персонажа, представляющий полное состояние персонажа для read-модели.
/// Используется в проекциях для отображения данных без раскрытия доменных агрегатов.
/// Содержит все основные характеристики, способности, инвентарь и текущее состояние.
/// </summary>
/// <param name="Id">Уникальный идентификатор персонажа.</param>
/// <param name="Name">Имя персонажа.</param>
/// <param name="MaxHitPoints">Максимальное количество хитов.</param>
/// <param name="HitPoints">Текущее количество хитов.</param>
/// <param name="TemporaryHitPoints">Количество временных хитов (по умолчанию 0).</param>
/// <param name="ArmorClass">Класс брони (AC) (по умолчанию 10).</param>
/// <param name="Speed">Скорость передвижения в футах (по умолчанию 30).</param>
/// <param name="ExperiencePoints">Накопленный опыт (по умолчанию 0).</param>
/// <param name="Level">Текущий уровень (по умолчанию 1).</param>
/// <param name="Race">Раса персонажа (пустая строка, если не выбрана).</param>
/// <param name="Class">Класс персонажа (пустая строка, если не выбран).</param>
/// <param name="Background">Предыстория персонажа (пустая строка, если не выбрана).</param>
/// <param name="ProficiencyBonus">Бонус мастерства (по умолчанию 2).</param>
/// <param name="AbilityScores">Словарь значений характеристик (название → значение).</param>
/// <param name="SkillProficiencies">Словарь владения навыками (название → есть/нет).</param>
/// <param name="SavingThrowProficiencies">Словарь владения спасбросками (характеристика → есть/нет).</param>
/// <param name="KnownSpells">Список известных заклинаний.</param>
/// <param name="MaxSpellSlots">Словарь максимального количества ячеек заклинаний по уровням.</param>
/// <param name="UsedSpellSlots">Словарь использованных ячеек заклинаний по уровням.</param>
/// <param name="HitDiceRemaining">Словарь оставшихся костей хитов по типам.</param>
/// <param name="MaxHitDice">Словарь максимального количества костей хитов по типам.</param>
/// <param name="DeathSaveSuccesses">Количество успешных спасбросков от смерти.</param>
/// <param name="DeathSaveFailures">Количество проваленных спасбросков от смерти.</param>
/// <param name="IsStable">Стабилизирован ли персонаж (при 0 хитов).</param>
/// <param name="IsDead">Мёртв ли персонаж.</param>
/// <param name="Conditions">Список активных состояний (например, "оглох").</param>
/// <param name="Resistances">Список сопротивлений урону.</param>
/// <param name="Vulnerabilities">Список уязвимостей к урону.</param>
/// <param name="Immunities">Список иммунитетов к урону.</param>
/// <param name="Equipment">Список экипированных предметов.</param>
/// <param name="Inventory">Список предметов в инвентаре.</param>
/// <param name="Feats">Список черт персонажа.</param>
/// <param name="Concentrating">Поддерживает ли концентрацию на заклинании.</param>
/// <param name="Gold">Количество золота.</param>
/// <param name="HitDice">Словарь костей хитов (тип → количество).</param>
/// <param name="SpellSlots">Словарь ячеек заклинаний (уровень → количество).</param>
public record CharacterDto(
    Guid Id,
    string Name,
    int MaxHitPoints,
    int HitPoints,
    int TemporaryHitPoints = 0,
    int ArmorClass = 10,
    int Speed = 30,
    int ExperiencePoints = 0,
    int Level = 1,
    string Race = "",
    string Class = "",
    string Background = "",
    int ProficiencyBonus = 2,
    Dictionary<string, int> AbilityScores = null!,
    IDictionary<string, bool> SkillProficiencies = null!,
    IDictionary<string, bool> SavingThrowProficiencies = null!,
    List<string> KnownSpells = null!,
    Dictionary<int, int> MaxSpellSlots = null!,
    Dictionary<int, int> UsedSpellSlots = null!,
    Dictionary<int, int> HitDiceRemaining = null!,
    Dictionary<int, int> MaxHitDice = null!,
    int DeathSaveSuccesses = 0,
    int DeathSaveFailures = 0,
    bool IsStable = false,
    bool IsDead = false,
    List<string> Conditions = null!,
    List<string> Resistances = null!,
    List<string> Vulnerabilities = null!,
    List<string> Immunities = null!,
    List<EquippedItemDto> Equipment = null!,
    List<InventoryItemDto> Inventory = null!,
    List<string> Feats = null!,
    bool Concentrating = false,
    int Gold = 0,
    Dictionary<int, int> HitDice = null!,
    Dictionary<int, int> SpellSlots = null!)
{
    /// <summary>
    /// Возвращает true, если персонаж без сознания: хиты ≤ 0, не мёртв и не стабилизирован.
    /// </summary>
    public bool IsUnconscious => HitPoints <= 0 && !IsDead && !IsStable;

    /// <summary>
    /// Возвращает true, если персонаж находится при смерти: хиты ≤ 0, не мёртв, не стабилизирован,
    /// и ещё есть шансы на спасброски (успехов и провалов меньше 3).
    /// </summary>
    public bool IsDying => HitPoints <= 0 && !IsDead && !IsStable && DeathSaveSuccesses < 3 && DeathSaveFailures < 3;

    /// <summary>
    /// Строковое представление класса брони для отображения.
    /// </summary>
    public string ArmorClassDisplay => ArmorClass.ToString();
}

/// <summary>
/// DTO для отображения текущего состояния хитов персонажа.
/// </summary>
/// <param name="Current">Текущие хиты.</param>
/// <param name="Max">Максимальные хиты.</param>
/// <param name="Temporary">Временные хиты.</param>
public record CharacterHitPointsDto(int Current, int Max, int Temporary);

/// <summary>
/// DTO боевых характеристик персонажа.
/// </summary>
/// <param name="ArmorClass">Класс брони.</param>
/// <param name="Speed">Скорость в футах.</param>
/// <param name="HitDiceRemaining">Словарь оставшихся костей хитов по типам.</param>
/// <param name="DeathSaveSuccesses">Успешные спасброски от смерти.</param>
/// <param name="DeathSaveFailures">Проваленные спасброски от смерти.</param>
/// <param name="IsStable">Стабилизирован ли персонаж.</param>
public record CharacterCombatStatsDto(int ArmorClass, int Speed, Dictionary<int, int> HitDiceRemaining, int DeathSaveSuccesses, int DeathSaveFailures, bool IsStable);

/// <summary>
/// DTO заклинаний персонажа.
/// </summary>
/// <param name="KnownSpells">Список известных заклинаний.</param>
/// <param name="MaxSpellSlots">Максимальное количество ячеек по уровням.</param>
/// <param name="UsedSpellSlots">Использованные ячейки по уровням.</param>
public record CharacterSpellsDto(List<string> KnownSpells, Dictionary<int, int> MaxSpellSlots, Dictionary<int, int> UsedSpellSlots);

/// <summary>
/// DTO статуса жизни/смерти персонажа.
/// </summary>
/// <param name="Status">Текстовый статус (например, "Жив", "При смерти", "Стабилен", "Мёртв").</param>
/// <param name="DeathSaveSuccesses">Успешные спасброски от смерти.</param>
/// <param name="DeathSaveFailures">Проваленные спасброски от смерти.</param>
public record CharacterDeathStatusDto(string Status, int DeathSaveSuccesses, int DeathSaveFailures);

/// <summary>
/// DTO защитных свойств персонажа (сопротивления, уязвимости, иммунитеты).
/// </summary>
/// <param name="Resistances">Список сопротивлений.</param>
/// <param name="Vulnerabilities">Список уязвимостей.</param>
/// <param name="Immunities">Список иммунитетов.</param>
public record CharacterDefensesDto(List<string> Resistances, List<string> Vulnerabilities, List<string> Immunities);

/// <summary>
/// Краткая сводка о персонаже для списков.
/// </summary>
/// <param name="Id">Идентификатор персонажа.</param>
/// <param name="Name">Имя.</param>
/// <param name="Level">Уровень.</param>
/// <param name="Class">Класс.</param>
/// <param name="Race">Раса.</param>
/// <param name="HitPoints">Текущие хиты.</param>
/// <param name="MaxHitPoints">Максимальные хиты.</param>
/// <param name="IsAlive">Жив ли персонаж.</param>
/// <param name="ArmorClass">Класс брони.</param>
public record CharacterSummaryDto(Guid Id, string Name, int Level, string Class, string Race, int HitPoints, int MaxHitPoints, bool IsAlive, int ArmorClass);

/// <summary>
/// DTO предмета в инвентаре.
/// </summary>
/// <param name="ItemId">Идентификатор предмета.</param>
/// <param name="Name">Название предмета.</param>
/// <param name="Quantity">Количество.</param>
public record InventoryItemDto(string ItemId, string Name, int Quantity);

/// <summary>
/// DTO экипированного предмета.
/// </summary>
/// <param name="ItemId">Идентификатор предмета.</param>
/// <param name="Slot">Слот экипировки (например, "Оружие", "Броня").</param>
/// <param name="Name">Название предмета.</param>
/// <param name="ArmorBonus">Бонус к броне.</param>
/// <param name="DamageBonus">Бонус к урону.</param>
public record EquippedItemDto(string ItemId, string Slot, string Name, int ArmorBonus, int DamageBonus);