// domain/commands/character_commands.cs
namespace dnd_game.Domain.Commands;

// ---------- Базовые (уже были) ----------
public record CreateCharacter(Guid CharacterId, string Name, int MaxHitPoints) : ICommand;
public record DealDamage(Guid CharacterId, int Amount, string DamageType = "bludgeoning") : ICommand;
public record HealCharacter(Guid CharacterId, int Amount) : ICommand;
public record UpdateCharacter(Guid CharacterId, string? Name, int? MaxHitPoints) : ICommand;

// ---------- Временные хиты ----------
public record SetTemporaryHitPoints(Guid CharacterId, int Amount) : ICommand;

// ---------- Опыт и уровень ----------
public record GainExperience(Guid CharacterId, int ExperiencePoints) : ICommand;
public record LevelUpCharacter(Guid CharacterId, int NewLevel) : ICommand;

// ---------- Характеристики ----------
public record SetAbilityScore(Guid CharacterId, string Ability, int Score) : ICommand;

// ---------- Раса, класс, предыстория ----------
public record ChooseRace(Guid CharacterId, string RaceId) : ICommand;
public record ChooseClass(Guid CharacterId, string ClassId) : ICommand;
public record ChooseBackground(Guid CharacterId, string BackgroundId) : ICommand;

// ---------- Владения навыками и спасбросками ----------
public record AddSkillProficiency(Guid CharacterId, string SkillName) : ICommand;
public record RemoveSkillProficiency(Guid CharacterId, string SkillName) : ICommand;
public record AddSavingThrowProficiency(Guid CharacterId, string Ability) : ICommand;
public record RemoveSavingThrowProficiency(Guid CharacterId, string Ability) : ICommand;

// ---------- Черты ----------
public record AddFeat(Guid CharacterId, string FeatId) : ICommand;
public record RemoveFeat(Guid CharacterId, string FeatId) : ICommand;

// ---------- Заклинания ----------
public record AddSpell(Guid CharacterId, string SpellId) : ICommand;
public record RemoveSpell(Guid CharacterId, string SpellId) : ICommand;
public record PrepareSpell(Guid CharacterId, string SpellId) : ICommand;
public record UnprepareSpell(Guid CharacterId, string SpellId) : ICommand;
public record UseSpellSlot(Guid CharacterId, int SlotLevel) : ICommand;
public record RestoreAllSpellSlots(Guid CharacterId) : ICommand;
public record SetSpellSlots(Guid CharacterId, Dictionary<int, int> MaxSlots) : ICommand;

// ---------- Кости хитов ----------
public record SetHitDice(Guid CharacterId, Dictionary<int, int> Dice) : ICommand;           // тип кости -> количество
public record RecoverHitDice(Guid CharacterId, Dictionary<int, int> Recovered) : ICommand;  // тип кости -> сколько восстановлено

// ---------- Состояния ----------
public record ApplyCondition(Guid CharacterId, string ConditionType, int DurationRounds) : ICommand;
public record RemoveCondition(Guid CharacterId, string ConditionType) : ICommand;

// ---------- Боевые параметры ----------
public record UpdateArmorClass(Guid CharacterId, int NewArmorClass) : ICommand;
public record UpdateSpeed(Guid CharacterId, int NewSpeed) : ICommand;

// ---------- Защиты ----------
public record AddResistance(Guid CharacterId, string DamageType) : ICommand;
public record RemoveResistance(Guid CharacterId, string DamageType) : ICommand;
public record AddVulnerability(Guid CharacterId, string DamageType) : ICommand;
public record RemoveVulnerability(Guid CharacterId, string DamageType) : ICommand;
public record AddImmunity(Guid CharacterId, string DamageType) : ICommand;
public record RemoveImmunity(Guid CharacterId, string DamageType) : ICommand;

// ---------- Экипировка и инвентарь ----------
public record EquipItem(Guid CharacterId, string ItemId, string Slot, string ItemName, int ArmorBonus = 0, int DamageBonus = 0) : ICommand;
public record UnequipItem(Guid CharacterId, string ItemId) : ICommand;
public record AddInventoryItem(Guid CharacterId, string ItemId, string ItemName, int Quantity = 1) : ICommand;
public record RemoveInventoryItem(Guid CharacterId, string ItemId, int Quantity = 1) : ICommand;

// ---------- Смерть и спасброски ----------
public record DeathSavingThrow(Guid CharacterId, int RollResult) : ICommand;
public record StabilizeCharacter(Guid CharacterId) : ICommand;
public record MarkCharacterDead(Guid CharacterId) : ICommand;
public record ReviveCharacter(Guid CharacterId, int HitPointsAfterRevive) : ICommand;

// ---------- Концентрация ----------
public record StartConcentration(Guid CharacterId, string SpellId) : ICommand;
public record EndConcentration(Guid CharacterId) : ICommand;

// ---------- Отдых (может использоваться напрямую персонажем) ----------
public record TakeShortRest(Guid CharacterId, List<(int HitDieType, int Roll, int ConstitutionModifier)>? HitDiceSpent) : ICommand;
public record TakeLongRest(Guid CharacterId) : ICommand;

// ---------- Аттунемент магических предметов ----------
public record AttuneItem(Guid CharacterId, string ItemId) : ICommand;
public record UnattuneItem(Guid CharacterId, string ItemId) : ICommand;

// ---------- Классовые умения ----------
public record UseClassFeature(Guid CharacterId, string FeatureId) : ICommand;
public record RechargeFeature(Guid CharacterId, string FeatureId) : ICommand;
// Спасбросок
public record MakeSavingThrow(Guid CharacterId, string AbilityType, int DifficultyClass, int RollResult) : ICommand;
// Временные хиты
public record UpdateTemporaryHitPoints(Guid CharacterId, int Amount) : ICommand;
// Бонус мастерства
public record UpdateProficiencyBonus(Guid CharacterId, int Bonus) : ICommand;
// Сброс спасбросков смерти
public record ResetDeathSavingThrows(Guid CharacterId) : ICommand;
public record CastSpell(Guid CharacterId, string SpellId, Guid? TargetId, int SpellSlotLevel) : ICommand;
// Дополнительные команды, используемые триггерами
public record GiveItemCommand(Guid CharacterId, string ItemId, string ItemName, int Quantity = 1) : ICommand;
public record SpawnMonsterCommand(string TemplateId, int X, int Y) : ICommand;
public record TeleportCommand(Guid CharacterId, int DestinationX, int DestinationY) : ICommand;
public record SetQuestFlagCommand(Guid CharacterId, string QuestId, string Flag, string Value) : ICommand;
public record StartDialogCommand(Guid InitiatorId, string DialogId) : ICommand;
public record PlaySoundCommand(string SoundName, int PositionX, int PositionY) : ICommand;
public record StartQuestCommand(Guid CharacterId, Guid QuestId) : ICommand;
public record StartDialogueCommand(Guid DialogueId, Guid NpcId, Guid CharacterId) : ICommand;
public record EndDialogueCommand(Guid DialogueId) : ICommand;
public record SpendGold(Guid CharacterId, int Amount) : ICommand;
public record AddGold(Guid CharacterId, int Amount) : ICommand;
public record IncreaseMaxHitPoints(Guid CharacterId, int Amount) : ICommand;
public record AddHitDie(Guid CharacterId, int HitDieType) : ICommand;
public record ClearAllConditionsCommand(Guid CharacterId) : ICommand;
public record SetGoldCommand(Guid CharacterId, int Amount) : ICommand;