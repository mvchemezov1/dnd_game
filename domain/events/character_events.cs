// domain/events/character_events.cs
namespace dnd_game.Domain.Events;

public record ProficiencyBonusUpdated(Guid CharacterId, int Bonus) : IDomainEvent;
public record SpellPrepared(Guid CharacterId, string SpellId) : IDomainEvent;
public record SpellUnprepared(Guid CharacterId, string SpellId) : IDomainEvent;
public record ClassFeatureUsed(Guid CharacterId, string FeatureId) : IDomainEvent;
public record ClassFeatureRecharged(Guid CharacterId, string FeatureId) : IDomainEvent;
public record ItemAttuned(Guid CharacterId, string ItemId) : IDomainEvent;
public record ItemUnattuned(Guid CharacterId, string ItemId) : IDomainEvent;
public record DeathSavingThrowsReset(Guid CharacterId) : IDomainEvent;

// ---------- Существующие события (оставлены без изменений) ----------
public record CharacterCreated(Guid CharacterId, string Name, int MaxHitPoints, DateTime OccurredOn) : IDomainEvent;
public record CharacterDamageTaken(Guid CharacterId, int Amount, DateTime OccurredOn) : IDomainEvent;
public record CharacterHealed(Guid CharacterId, int Amount, DateTime OccurredOn) : IDomainEvent;
public record CharacterDied(Guid CharacterId, DateTime OccurredOn) : IDomainEvent;
public record CharacterUpdated(Guid CharacterId, string? Name, int? MaxHitPoints, DateTime OccurredOn) : IDomainEvent;

// ---------- Временные хиты ----------
public record TemporaryHitPointsSet(Guid CharacterId, int Amount) : IDomainEvent;

// ---------- Опыт и уровень ----------
public record ExperienceGained(Guid CharacterId, int Amount) : IDomainEvent;
public record CharacterLevelUp(Guid CharacterId, int NewLevel, int NewProficiencyBonus) : IDomainEvent;

// ---------- Характеристики ----------
public record AbilityScoreSet(Guid CharacterId, string Ability, int Score) : IDomainEvent;

// ---------- Раса, класс, предыстория ----------
public record RaceChosen(Guid CharacterId, string Race) : IDomainEvent;
public record ClassChosen(Guid CharacterId, string ClassName) : IDomainEvent;
public record BackgroundChosen(Guid CharacterId, string BackgroundName) : IDomainEvent;

// ---------- Владения навыками и спасбросками ----------
public record SkillProficiencyAdded(Guid CharacterId, string Skill) : IDomainEvent;
public record SkillProficiencyRemoved(Guid CharacterId, string Skill) : IDomainEvent;
public record SavingThrowProficiencyAdded(Guid CharacterId, string Ability) : IDomainEvent;
public record SavingThrowProficiencyRemoved(Guid CharacterId, string Ability) : IDomainEvent;

// ---------- Черты ----------
public record FeatAdded(Guid CharacterId, string FeatName) : IDomainEvent;
public record FeatRemoved(Guid CharacterId, string FeatName) : IDomainEvent;

// ---------- Заклинания ----------
public record SpellAdded(Guid CharacterId, string SpellId) : IDomainEvent;
public record SpellRemoved(Guid CharacterId, string SpellId) : IDomainEvent;
public record SpellSlotsSet(Guid CharacterId, Dictionary<int, int> MaxSlots) : IDomainEvent;   // уровень ячейки -> макс. кол-во
public record SpellSlotUsed(Guid CharacterId, int SlotLevel) : IDomainEvent;
public record SpellSlotsRestored(Guid CharacterId, int SlotLevel, int RestoredCount) : IDomainEvent;

// ---------- Кости хитов ----------
public record HitDiceSet(Guid CharacterId, Dictionary<int, int> Dice) : IDomainEvent;        // тип кости -> количество
public record HitDieSpent(Guid CharacterId, int HitDieType, int HealedAmount) : IDomainEvent; // HealedAmount = roll + con mod
public record HitDiceRecovered(Guid CharacterId, Dictionary<int, int> Recovered) : IDomainEvent;

// ---------- Состояния ----------
public record ConditionApplied(Guid CharacterId, string Condition) : IDomainEvent;
public record ConditionRemoved(Guid CharacterId, string Condition) : IDomainEvent;
public record AllConditionsCleared(Guid CharacterId) : IDomainEvent;

// ---------- Боевые параметры ----------
public record ArmorClassUpdated(Guid CharacterId, int NewArmorClass) : IDomainEvent;
public record SpeedUpdated(Guid CharacterId, int NewSpeed) : IDomainEvent;

// ---------- Защиты ----------
public record ResistanceAdded(Guid CharacterId, string DamageType) : IDomainEvent;
public record ResistanceRemoved(Guid CharacterId, string DamageType) : IDomainEvent;
public record VulnerabilityAdded(Guid CharacterId, string DamageType) : IDomainEvent;
public record VulnerabilityRemoved(Guid CharacterId, string DamageType) : IDomainEvent;
public record ImmunityAdded(Guid CharacterId, string DamageType) : IDomainEvent;
public record ImmunityRemoved(Guid CharacterId, string DamageType) : IDomainEvent;

// ---------- Экипировка и инвентарь ----------
public record ItemEquipped(Guid CharacterId, string ItemId, string Slot, string ItemName, int ArmorBonus = 0, int DamageBonus = 0) : IDomainEvent;
public record ItemUnequipped(Guid CharacterId, string ItemId) : IDomainEvent;
public record InventoryItemAdded(Guid CharacterId, string ItemId, string ItemName, int Quantity = 1) : IDomainEvent;
public record InventoryItemRemoved(Guid CharacterId, string ItemId, int Quantity = 1) : IDomainEvent;

// ---------- Спасброски от смерти и жизненные состояния ----------
public record DeathSavingThrowSuccess(Guid CharacterId) : IDomainEvent;
public record DeathSavingThrowFailure(Guid CharacterId) : IDomainEvent;
public record CharacterStabilized(Guid CharacterId) : IDomainEvent;
public record CharacterRevived(Guid CharacterId, int NewHitPoints) : IDomainEvent;

// ---------- Концентрация ----------
public record ConcentrationStarted(Guid CharacterId, string SpellId) : IDomainEvent;
public record ConcentrationEnded(Guid CharacterId, string SpellId, string Reason) : IDomainEvent;

// ---------- Действия с золотом ----------
public record GoldAdded(Guid CharacterId, int Amount) : IDomainEvent;
public record GoldSpent(Guid CharacterId, int Amount) : IDomainEvent;
public record GoldSet(Guid CharacterId, int Amount) : IDomainEvent;