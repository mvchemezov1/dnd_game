// application/projections/character_dto.cs
namespace dnd_game.Application.Projections;

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
    Dictionary<int, int> HitDice = null!
,
    Dictionary<int, int> SpellSlots = null!)
{
    // Вспомогательные вычисляемые свойства
    public bool IsUnconscious => HitPoints <= 0 && !IsDead && !IsStable;
    public bool IsDying => HitPoints <= 0 && !IsDead && !IsStable && DeathSaveSuccesses < 3 && DeathSaveFailures < 3;
    public string ArmorClassDisplay => ArmorClass.ToString();
}

public record CharacterHitPointsDto(int Current, int Max, int Temporary);
public record CharacterCombatStatsDto(int ArmorClass, int Speed, Dictionary<int, int> HitDiceRemaining, int DeathSaveSuccesses, int DeathSaveFailures, bool IsStable);
public record CharacterSpellsDto(List<string> KnownSpells, Dictionary<int, int> MaxSpellSlots, Dictionary<int, int> UsedSpellSlots);
public record CharacterDeathStatusDto(string Status, int DeathSaveSuccesses, int DeathSaveFailures);
public record CharacterDefensesDto(List<string> Resistances, List<string> Vulnerabilities, List<string> Immunities);
public record CharacterSummaryDto(Guid Id, string Name, int Level, string Class, string Race, int HitPoints, int MaxHitPoints, bool IsAlive, int ArmorClass);
public record InventoryItemDto(string ItemId, string Name, int Quantity);
public record EquippedItemDto(string ItemId, string Slot, string Name, int ArmorBonus, int DamageBonus);