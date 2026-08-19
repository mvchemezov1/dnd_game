// domain/aggregates/character_aggregate.cs
using dnd_game.Domain.Events;
using dnd_game.Domain.Exceptions;
using dnd_game.Domain.Rules;
using Microsoft.Extensions.Logging;

namespace dnd_game.Domain.Aggregates
{
    public class CharacterAggregate : AggregateRoot
    {
        // ---------- Основные параметры ----------
        public string Name { get; private set; } = string.Empty;
        public int HitPoints { get; private set; }
        public int MaxHitPoints { get; private set; }
        public int TemporaryHitPoints { get; private set; }
        public int ArmorClass { get; private set; } = 10;
        public int Speed { get; private set; } = 30;
        public int PositionX { get; private set; }
        public int PositionY { get; private set; }
        public int ExperiencePoints { get; private set; }
        public int Level { get; private set; } = 1;
        public int ProficiencyBonus { get; private set; } = 2;
        public int Gold { get; private set; }
        public string Race { get; private set; } = string.Empty;
        public string Class { get; private set; } = string.Empty;
        public string Background { get; private set; } = string.Empty;
        private string _currentRestType = "";

        // ---------- Методы управления золотом ----------

        /// <summary>
        /// Добавить золото персонажу.
        /// </summary>
        public void AddGold(int amount)
        {
            if (amount <= 0) throw new ArgumentException("Amount must be positive.", nameof(amount));
            if (IsDead) throw new InvalidOperationException("Cannot add gold to a dead character.");
            ApplyChange(new GoldAdded(Id, amount));
        }

        /// <summary>
        /// Потратить золото (уменьшить баланс).
        /// </summary>
        public void SpendGold(int amount)
        {
            if (amount <= 0) throw new ArgumentException("Amount must be positive.", nameof(amount));
            if (IsDead) throw new InvalidOperationException("Cannot spend gold while dead.");
            if (Gold < amount) throw new InvalidOperationException($"Insufficient gold. Required: {amount}, available: {Gold}.");
            ApplyChange(new GoldSpent(Id, amount));
        }

        /// <summary>
        /// Установить точное количество золота (для административных целей).
        /// </summary>
        public void SetGold(int amount)
        {
            if (amount < 0) throw new ArgumentException("Gold cannot be negative.", nameof(amount));
            ApplyChange(new GoldSet(Id, amount));
        }

        public void StartRest(string restType)
        {
            _currentRestType = restType;
            ApplyChange(new RestStarted(Id, restType, DateTime.UtcNow));
        }

        public void InterruptRest(string interruptionType)
        {
            ApplyChange(new RestInterrupted(Id, interruptionType, DateTime.UtcNow));
        }

        public void EndRest()
        {
            int hpRestored = _currentRestType == "Long" ? MaxHitPoints - HitPoints : 0;
            ApplyChange(new RestCompleted(Id, _currentRestType, hpRestored, DateTime.UtcNow));
            _currentRestType = "";
        }

        public void LevelUp(int newLevel)
        {
            if (newLevel <= Level || newLevel > 20) throw new ArgumentException("Invalid new level.");
            int newProfBonus = 2 + (int)Math.Floor((newLevel - 1) / 4.0);
            ApplyChange(new CharacterLevelUp(Id, newLevel, newProfBonus));
        }

        public void CastSpell(string spellId, int spellSlotLevel)
        {
            // Проверка ячеек заклинаний (упрощённо, без валидации наличия заклинания)
            if (!MaxSpellSlots.ContainsKey(spellSlotLevel))
                throw new InvalidOperationException("No such spell slot level.");
            ApplyChange(new SpellSlotUsed(Id, spellSlotLevel));
        }

        public void TakeShortRest(int v)
        {
            // hitDiceSpent – количество потраченных костей (не используется напрямую, но передаётся)
            // Реальная реализация: персонаж тратит кости через SpendHitDie по одной.
            // Здесь просто фиксируем факт короткого отдыха (можно породить событие RestCompleted(Short))
            ApplyChange(new RestCompleted(Id, "Short", 0, DateTime.UtcNow));
        }

        public void TakeLongRest()
        {
            ApplyChange(new RestCompleted(Id, "Long", MaxHitPoints - HitPoints, DateTime.UtcNow));
            // + восстановление ячеек, костей хитов и т.д. — это уже делается в ApplyEvent
        }

        public void MakeSavingThrow(string abilityType, int difficultyClass, int rollResult)
        {
            // Результат проверки (успех/провал) обрабатывается внешним кодом.
            // Здесь фиксируем событие попытки спасброска.
            // Упрощённо: создаём событие только если способность известна.
            if (!AbilityScores.ContainsKey(abilityType)) throw new ArgumentException("Invalid ability.");
            // Предположим, что событие будет обработано сагой/внешним правилом.
            // Например:
            // ApplyChange(new SavingThrowAttempted(Id, abilityType, difficultyClass, rollResult));
        }

        public void MakeDeathSavingThrow(int rollResult)
        {
            bool success = rollResult >= 10;
            if (success)
                ApplyChange(new DeathSavingThrowSuccess(Id));
            else
                ApplyChange(new DeathSavingThrowFailure(Id));
        }

        public void SetProficiencyBonus(int bonus)
        {
            if (bonus < 2 || bonus > 6) throw new ArgumentException("Invalid proficiency bonus.");
            ApplyChange(new ProficiencyBonusUpdated(Id, bonus));
        }

        public void PrepareSpell(string spellId)
        {
            // Проверка, что заклинание известно, и что не превышен лимит подготовки.
            if (!KnownSpells.Contains(spellId)) throw new InvalidOperationException("Spell not known.");
            // Событие: заклинание подготовлено (может добавляться в список подготовленных)
            // Здесь для простоты используем общее событие, которое проекция обрабатывает.
            ApplyChange(new SpellPrepared(Id, spellId));
        }

        public void UnprepareSpell(string spellId)
        {
            ApplyChange(new SpellUnprepared(Id, spellId));
        }

        public void UseClassFeature(string featureId)
        {
            // Отправляем событие использования умения. Проверка доступности — вне агрегата.
            ApplyChange(new ClassFeatureUsed(Id, featureId));
        }

        public void RechargeFeature(string featureId)
        {
            ApplyChange(new ClassFeatureRecharged(Id, featureId));
        }

        public void AttuneItem(string itemId)
        {
            // Проверить лимит аттунемента (3)
            // Упрощённо
            ApplyChange(new ItemAttuned(Id, itemId));
        }

        public void UnattuneItem(string itemId)
        {
            ApplyChange(new ItemUnattuned(Id, itemId));
        }

        public void ResetDeathSavingThrows()
        {
            ApplyChange(new DeathSavingThrowsReset(Id));
        }

        // ---------- Характеристики ----------
        public Dictionary<string, int> AbilityScores { get; private set; } = new()
        {
            {"Strength", 10}, {"Dexterity", 10}, {"Constitution", 10},
            {"Intelligence", 10}, {"Wisdom", 10}, {"Charisma", 10}
        };

        // ---------- Владения ----------
        public List<string> SkillProficiencies { get; private set; } = [];
        public List<string> SavingThrowProficiencies { get; private set; } = [];

        // ---------- Черты ----------
        public List<string> Feats { get; private set; } = [];

        // ---------- Заклинания ----------
        public List<string> KnownSpells { get; private set; } = [];
        public Dictionary<int, int> MaxSpellSlots { get; private set; } = [];   // уровень ячейки -> макс. кол-во
        public Dictionary<int, int> UsedSpellSlots { get; private set; } = []; // уровень ячейки -> использовано

        // ---------- Кости хитов ----------
        // ключ – тип кости (6,8,10,12), значение – оставшееся количество
        public Dictionary<int, int> HitDiceRemaining { get; private set; } = [];
        public Dictionary<int, int> MaxHitDice { get; private set; } = [];     // максимум костей каждого типа (обычно = уровню в классе)

        // ---------- Смерть и спасброски ----------
        public bool IsDead { get; private set; }
        public bool IsStable { get; private set; }
        public int DeathSaveSuccesses { get; private set; }
        public int DeathSaveFailures { get; private set; }

        // ---------- Состояния ----------
        public List<string> Conditions { get; private set; } = [];

        // ---------- Защиты ----------
        public List<string> Resistances { get; private set; } = [];
        public List<string> Vulnerabilities { get; private set; } = [];
        public List<string> Immunities { get; private set; } = [];

        // ---------- Экипировка и инвентарь ----------
        public List<EquippedItem> Equipment { get; private set; } = [];
        public List<InventoryItem> Inventory { get; private set; } = [];

        // ---------- Концентрация ----------
        public bool Concentrating { get; private set; }
        public string? ConcentratingOnSpellId { get; private set; }

        // ---------- Прочее ----------
        public bool IsAlive => !IsDead;
        public bool IsUnconscious => HitPoints <= 0 && !IsDead && !IsStable;


        // ---------- Конструкторы ----------
        public CharacterAggregate(Guid id, string name, int maxHp = 10)
        {
            ApplyChange(new CharacterCreated(id, name, maxHp, DateTime.UtcNow));
        }

        public CharacterAggregate() { }

        // ---------- Применение событий ----------
        protected override void ApplyEvent(IDomainEvent @event)
        {
            switch (@event)
            {
                case CharacterCreated e:
                    Id = e.CharacterId;
                    Name = e.Name;
                    MaxHitPoints = e.MaxHitPoints;
                    HitPoints = e.MaxHitPoints;
                    break;
                case CharacterUpdated e:
                    if (e.Name != null) Name = e.Name;
                    if (e.MaxHitPoints.HasValue)
                    {
                        MaxHitPoints = e.MaxHitPoints.Value;
                        if (HitPoints > MaxHitPoints) HitPoints = MaxHitPoints;
                    }
                    break;
                case CharacterDamageTaken e:
                    ApplyDamage(e.Amount);
                    break;
                case CharacterHealed e:
                    HealHitPoints(e.Amount);
                    break;
                case TemporaryHitPointsSet e:
                    TemporaryHitPoints = Math.Max(TemporaryHitPoints, e.Amount);
                    break;
                case ExperienceGained e:
                    ExperiencePoints += e.Amount;
                    break;
                case CharacterLevelUp e:
                    Level = e.NewLevel;
                    ProficiencyBonus = e.NewProficiencyBonus;
                    break;
                case AbilityScoreSet e:
                    AbilityScores[e.Ability] = Math.Clamp(e.Score, 1, 30);
                    break;
                case RaceChosen e: Race = e.Race; break;
                case ClassChosen e: Class = e.ClassName; break;
                case BackgroundChosen e: Background = e.BackgroundName; break;
                case SkillProficiencyAdded e: if (!SkillProficiencies.Contains(e.Skill)) SkillProficiencies.Add(e.Skill); break;
                case SkillProficiencyRemoved e: SkillProficiencies.Remove(e.Skill); break;
                case SavingThrowProficiencyAdded e: if (!SavingThrowProficiencies.Contains(e.Ability)) SavingThrowProficiencies.Add(e.Ability); break;
                case SavingThrowProficiencyRemoved e: SavingThrowProficiencies.Remove(e.Ability); break;
                case FeatAdded e: if (!Feats.Contains(e.FeatName)) Feats.Add(e.FeatName); break;
                case FeatRemoved e: Feats.Remove(e.FeatName); break;
                case SpellAdded e: if (!KnownSpells.Contains(e.SpellId)) KnownSpells.Add(e.SpellId); break;
                case SpellRemoved e: KnownSpells.Remove(e.SpellId); break;
                case SpellSlotsSet e:
                    {
                        MaxSpellSlots = new Dictionary<int, int>(e.MaxSlots);
                        UsedSpellSlots = e.MaxSlots.ToDictionary(kvp => kvp.Key, _ => 0);
                        break;
                    }
                case SpellSlotUsed e:
                    if (UsedSpellSlots.TryGetValue(e.SlotLevel, out int value))
                        UsedSpellSlots[e.SlotLevel] = ++value;
                    break;
                case SpellSlotsRestored e:
                    {
                        foreach (var slotLevel in UsedSpellSlots.Keys.ToList())
                        {
                            if (MaxSpellSlots.TryGetValue(slotLevel, out int maxSlots))
                                UsedSpellSlots[slotLevel] = 0;
                        }
                        break;
                    }
                case HitDiceSet e:
                    {
                        HitDiceRemaining = new Dictionary<int, int>(e.Dice);
                        MaxHitDice = new Dictionary<int, int>(e.Dice);
                        break;
                    }
                case HitDieSpent e:
                    {
                        if (HitDiceRemaining.TryGetValue(e.HitDieType, out int remaining))
                            HitDiceRemaining[e.HitDieType] = Math.Max(0, remaining - 1);
                        break;
                    }
                case HitDiceRecovered e:
                    {
                        foreach (var kvp in e.Recovered)
                        {
                            if (HitDiceRemaining.TryGetValue(kvp.Key, out int currentRemaining))
                            {
                                int maxForType = MaxHitDice.TryGetValue(kvp.Key, out int maxVal) ? maxVal : 0;
                                HitDiceRemaining[kvp.Key] = Math.Min(maxForType, currentRemaining + kvp.Value);
                            }
                        }
                        break;
                    }
                case ConditionApplied e:
                    if (!Conditions.Contains(e.Condition)) Conditions.Add(e.Condition);
                    break;
                case ConditionRemoved e:
                    Conditions.Remove(e.Condition);
                    break;
                case AllConditionsCleared e:
                    Conditions.Clear();
                    break;
                case ArmorClassUpdated e: ArmorClass = e.NewArmorClass; break;
                case SpeedUpdated e: Speed = e.NewSpeed; break;
                case CharacterMovedToPosition e: PositionX = e.TargetX; PositionY = e.TargetY; break;
                case ResistanceAdded e: if (!Resistances.Contains(e.DamageType)) Resistances.Add(e.DamageType); break;
                case VulnerabilityAdded e: if (!Vulnerabilities.Contains(e.DamageType)) Vulnerabilities.Add(e.DamageType); break;
                case ImmunityAdded e: if (!Immunities.Contains(e.DamageType)) Immunities.Add(e.DamageType); break;
                case ResistanceRemoved e: Resistances.Remove(e.DamageType); break;
                case VulnerabilityRemoved e: Vulnerabilities.Remove(e.DamageType); break;
                case ImmunityRemoved e: Immunities.Remove(e.DamageType); break;
                case ItemEquipped e:
                    Equipment.RemoveAll(i => i.Slot == e.Slot);
                    Equipment.Add(new EquippedItem { ItemId = e.ItemId, Slot = e.Slot, Name = e.ItemName, ArmorBonus = e.ArmorBonus, DamageBonus = e.DamageBonus });
                    break;
                case ItemUnequipped e:
                    Equipment.RemoveAll(i => i.ItemId == e.ItemId);
                    break;
                case InventoryItemAdded e:
                    var existing = Inventory.FirstOrDefault(i => i.ItemId == e.ItemId);
                    if (existing != null) existing.Quantity += e.Quantity;
                    else Inventory.Add(new InventoryItem { ItemId = e.ItemId, Name = e.ItemName, Quantity = e.Quantity });
                    break;
                case InventoryItemRemoved e:
                    var invItem = Inventory.FirstOrDefault(i => i.ItemId == e.ItemId);
                    if (invItem != null)
                    {
                        invItem.Quantity -= e.Quantity;
                        if (invItem.Quantity <= 0) Inventory.Remove(invItem);
                    }
                    break;
                case DeathSavingThrowSuccess e:
                    DeathSaveSuccesses = Math.Min(DeathSaveSuccesses + 1, 3);
                    if (DeathSaveSuccesses >= 3) IsStable = true;
                    break;
                case DeathSavingThrowFailure e:
                    DeathSaveFailures = Math.Min(DeathSaveFailures + 1, 3);
                    if (DeathSaveFailures >= 3) IsDead = true;
                    break;
                case CharacterStabilized e:
                    IsStable = true;
                    DeathSaveSuccesses = 0;
                    DeathSaveFailures = 0;
                    break;
                case CharacterDied e:
                    IsDead = true;
                    break;
                case CharacterRevived e:
                    IsDead = false;
                    HitPoints = e.NewHitPoints;
                    DeathSaveSuccesses = 0;
                    DeathSaveFailures = 0;
                    IsStable = false;
                    break;
                case ConcentrationStarted e:
                    Concentrating = true;
                    ConcentratingOnSpellId = e.SpellId;
                    break;
                case ConcentrationEnded e:
                    Concentrating = false;
                    ConcentratingOnSpellId = null;
                    break;
                case GoldAdded e:
                    Gold += e.Amount;
                    break;
                case GoldSpent e:
                    Gold -= e.Amount;
                    break;
                case GoldSet e:
                    Gold = e.Amount;
                    break;
            }
        }

        // ---------- Приватные методы модификации (без событий) ----------
        private void ApplyDamage(int amount)
        {
            int remaining = amount;
            if (TemporaryHitPoints > 0)
            {
                int absorbed = Math.Min(TemporaryHitPoints, remaining);
                TemporaryHitPoints -= absorbed;
                remaining -= absorbed;
            }
            HitPoints = Math.Max(0, HitPoints - remaining);
            if (HitPoints == 0 && !IsDead)
            {
                // Персонаж теряет сознание, может начать умирать
                // Эффект будет обработан внешними правилами через события DeathSavingThrow и т.д.
            }
        }

        private void HealHitPoints(int amount)
        {
            if (HitPoints > 0 || IsStable)
            {
                HitPoints = Math.Min(HitPoints + amount, MaxHitPoints);
                if (HitPoints > 0)
                {
                    IsStable = false;
                    DeathSaveSuccesses = 0;
                    DeathSaveFailures = 0;
                }
            }
        }

        // ---------- Инварианты ----------
        public override void EnsureInvariants()
        {
            if (HitPoints < 0) HitPoints = 0;
            if (HitPoints > MaxHitPoints) HitPoints = MaxHitPoints;
            if (Level < 1) Level = 1;
            if (Level > 20) throw new RuleViolation("Level", "Level cannot exceed 20.");
            foreach (var score in AbilityScores.Values)
                if (score < 1 || score > 30)
                    throw new RuleViolation("AbilityScore", "Ability scores must be between 1 and 30.");
            foreach (var slot in UsedSpellSlots)
                if (MaxSpellSlots.TryGetValue(slot.Key, out int value) && slot.Value > value)
                    throw new RuleViolation("SpellSlots", "Used spell slots exceed maximum.");
        }

        // ---------- Команды (публичные методы) ----------

        public void TakeDamage(int amount)
        {
            if (amount <= 0) throw new ArgumentException("Damage must be positive");
            if (IsDead) throw new RuleViolation("Character", "Cannot damage a dead character.");
            ApplyChange(new CharacterDamageTaken(Id, amount, DateTime.UtcNow));
        }

        public void Heal(int amount)
        {
            if (amount <= 0) throw new ArgumentException("Heal must be positive");
            if (IsDead) throw new RuleViolation("Character", "Cannot heal a dead character.");
            ApplyChange(new CharacterHealed(Id, amount, DateTime.UtcNow));
        }

        public void Update(string? name, int? maxHp)
        {
            if (name == null && maxHp == null) throw new ArgumentException("At least one field must be provided");
            if (name != null && !ValidationRules.IsValidCharacterName(name))
                throw new RuleViolation("Validation", "Invalid character name");
            ApplyChange(new CharacterUpdated(Id, name, maxHp, DateTime.UtcNow));
        }

        public void SetTemporaryHitPoints(int amount)
        {
            if (amount < 0) throw new ArgumentException("Temporary HP cannot be negative.");
            ApplyChange(new TemporaryHitPointsSet(Id, amount));
        }

        public void GainExperience(int amount)
        {
            if (amount <= 0) throw new ArgumentException("Experience must be positive.");
            ApplyChange(new ExperienceGained(Id, amount));
        } 

        public void SetAbilityScore(string ability, int score)
        {
            if (!AbilityScores.ContainsKey(ability)) throw new ArgumentException("Invalid ability.");
            ApplyChange(new AbilityScoreSet(Id, ability, score));
        }

        public void ChooseRace(string race)
        {
            if (string.IsNullOrWhiteSpace(race)) throw new ArgumentException("Race cannot be empty.");
            ApplyChange(new RaceChosen(Id, race));
        }

        public void ChooseClass(string className)
        {
            if (string.IsNullOrWhiteSpace(className)) throw new ArgumentException("Class cannot be empty.");
            ApplyChange(new ClassChosen(Id, className));
        }

        public void ChooseBackground(string backgroundName)
        {
            if (string.IsNullOrWhiteSpace(backgroundName)) throw new ArgumentException("Background cannot be empty.");
            ApplyChange(new BackgroundChosen(Id, backgroundName));
        }

        public void AddSkillProficiency(string skill)
        {
            if (SkillProficiencies.Contains(skill)) throw new InvalidOperationException("Already proficient.");
            ApplyChange(new SkillProficiencyAdded(Id, skill));
        }

        public void RemoveSkillProficiency(string skill)
        {
            if (!SkillProficiencies.Contains(skill)) throw new InvalidOperationException("Not proficient.");
            ApplyChange(new SkillProficiencyRemoved(Id, skill));
        }

        public void AddSavingThrowProficiency(string ability)
        {
            if (SavingThrowProficiencies.Contains(ability)) throw new InvalidOperationException("Already proficient.");
            ApplyChange(new SavingThrowProficiencyAdded(Id, ability));
        }

        public void RemoveSavingThrowProficiency(string ability)
        {
            if (!SavingThrowProficiencies.Contains(ability)) throw new InvalidOperationException("Not proficient.");
            ApplyChange(new SavingThrowProficiencyRemoved(Id, ability));
        }

        public void AddFeat(string featName)
        {
            if (Feats.Contains(featName)) throw new InvalidOperationException("Feat already known.");
            ApplyChange(new FeatAdded(Id, featName));
        }

        public void RemoveFeat(string featName)
        {
            if (!Feats.Contains(featName)) throw new InvalidOperationException("Feat not known.");
            ApplyChange(new FeatRemoved(Id, featName));
        }

        public void AddSpell(string spellId)
        {
            if (KnownSpells.Contains(spellId)) throw new InvalidOperationException("Spell already known.");
            ApplyChange(new SpellAdded(Id, spellId));
        }

        public void RemoveSpell(string spellId)
        {
            if (!KnownSpells.Contains(spellId)) throw new InvalidOperationException("Spell not known.");
            ApplyChange(new SpellRemoved(Id, spellId));
        }

        public void SetSpellSlots(Dictionary<int, int> maxSlots)
        {
            ApplyChange(new SpellSlotsSet(Id, maxSlots));
        }

        public void UseSpellSlot(int slotLevel)
        {
            if (!MaxSpellSlots.TryGetValue(slotLevel, out int maxSlots))
                throw new InvalidOperationException("No such spell slot level.");
            int used = UsedSpellSlots.TryGetValue(slotLevel, out int current) ? current : 0;
            if (used >= maxSlots)
                throw new InvalidOperationException("No available spell slots of this level.");
            ApplyChange(new SpellSlotUsed(Id, slotLevel));
        }

        public void RestoreAllSpellSlots()
        {
            foreach (var kvp in MaxSpellSlots)
                ApplyChange(new SpellSlotsRestored(Id, kvp.Key, kvp.Value));
        }

        public void SetHitDice(Dictionary<int, int> dice)
        {
            ApplyChange(new HitDiceSet(Id, dice));
        }

        public void SpendHitDie(int hitDieType, int roll, int constitutionModifier)
        {
            if (!HitDiceRemaining.TryGetValue(hitDieType, out int value) || value <= 0)
                throw new InvalidOperationException("No hit dice of that type remaining.");
            if (HitPoints <= 0 && !IsStable) throw new InvalidOperationException("Cannot spend hit dice while dying.");
            int healed = roll + constitutionModifier;
            ApplyChange(new HitDieSpent(Id, hitDieType, healed));
        }

        public void RecoverHitDice(Dictionary<int, int> recovered)
        {
            ApplyChange(new HitDiceRecovered(Id, recovered));
        }

        public void ApplyCondition(string condition, int durationRounds)
        {
            if (string.IsNullOrWhiteSpace(condition)) throw new ArgumentException("Condition cannot be empty.");
            ApplyChange(new ConditionApplied(Id, condition));
        }

        public void RemoveCondition(string condition)
        {
            if (!Conditions.Contains(condition)) throw new InvalidOperationException("Condition not present.");
            ApplyChange(new ConditionRemoved(Id, condition));
        }

        /// <summary>
        /// Очистить все активные состояния (условия) персонажа.
        /// </summary>
        public void ClearAllConditions()
        {
            if (Conditions.Count == 0)
                throw new InvalidOperationException("No conditions to clear.");
            ApplyChange(new AllConditionsCleared(Id));
        }

        public void UpdateArmorClass(int newAC)
        {
            if (newAC < 0) throw new ArgumentException("Armor class cannot be negative.");
            ApplyChange(new ArmorClassUpdated(Id, newAC));
        }

        public void UpdateSpeed(int newSpeed)
        {
            if (newSpeed < 0) throw new ArgumentException("Speed cannot be negative.");
            ApplyChange(new SpeedUpdated(Id, newSpeed));
        }

        public void AddResistance(string damageType)
        {
            if (Resistances.Contains(damageType)) throw new InvalidOperationException("Already resistant.");
            ApplyChange(new ResistanceAdded(Id, damageType));
        }

        public void RemoveResistance(string damageType)
        {
            if (!Resistances.Contains(damageType)) throw new InvalidOperationException("Not resistant.");
            ApplyChange(new ResistanceRemoved(Id, damageType));
        }

        public void AddVulnerability(string damageType)
        {
            if (Vulnerabilities.Contains(damageType)) throw new InvalidOperationException("Already vulnerable.");
            ApplyChange(new VulnerabilityAdded(Id, damageType));
        }

        public void RemoveVulnerability(string damageType)
        {
            if (!Vulnerabilities.Contains(damageType)) throw new InvalidOperationException("Not vulnerable.");
            ApplyChange(new VulnerabilityRemoved(Id, damageType));
        }

        public void AddImmunity(string damageType)
        {
            if (Immunities.Contains(damageType)) throw new InvalidOperationException("Already immune.");
            ApplyChange(new ImmunityAdded(Id, damageType));
        }

        public void RemoveImmunity(string damageType)
        {
            if (!Immunities.Contains(damageType)) throw new InvalidOperationException("Not immune.");
            ApplyChange(new ImmunityRemoved(Id, damageType));
        }

        public void EquipItem(string itemId, string slot, string itemName, int armorBonus = 0, int damageBonus = 0)
        {
            if (string.IsNullOrWhiteSpace(slot)) throw new ArgumentException("Slot required.");
            ApplyChange(new ItemEquipped(Id, itemId, slot, itemName, armorBonus, damageBonus));
        }

        public void UnequipItem(string itemId)
        {
            if (!Equipment.Any(e => e.ItemId == itemId)) throw new InvalidOperationException("Item not equipped.");
            ApplyChange(new ItemUnequipped(Id, itemId));
        }

        public void AddInventoryItem(string itemId, string itemName, int quantity = 1)
        {
            if (quantity <= 0) throw new ArgumentException("Quantity must be positive.");
            ApplyChange(new InventoryItemAdded(Id, itemId, itemName, quantity));
        }

        public void RemoveInventoryItem(string itemId, int quantity = 1)
        {
            var inv = Inventory.FirstOrDefault(i => i.ItemId == itemId);
            if (inv == null || inv.Quantity < quantity) throw new InvalidOperationException("Not enough items.");
            ApplyChange(new InventoryItemRemoved(Id, itemId, quantity));
        }

        public void DeathSavingThrow(bool success)
        {
            if (HitPoints > 0 || IsDead || IsStable)
                throw new InvalidOperationException("Death saving throws only while dying.");
            if (success)
                ApplyChange(new DeathSavingThrowSuccess(Id));
            else
                ApplyChange(new DeathSavingThrowFailure(Id));
        }

        public void Stabilize()
        {
            if (HitPoints > 0 || IsDead || IsStable) throw new InvalidOperationException("Not dying.");
            ApplyChange(new CharacterStabilized(Id));
        }

        public void MarkDead()
        {
            if (IsDead) throw new InvalidOperationException("Already dead.");
            ApplyChange(new CharacterDied(Id, DateTime.UtcNow));
        }

        public void Revive(int newHitPoints)
        {
            if (!IsDead) throw new InvalidOperationException("Character is not dead.");
            if (newHitPoints <= 0) throw new ArgumentException("Must have positive HP after revive.");
            ApplyChange(new CharacterRevived(Id, newHitPoints));
        }

        public void StartConcentration(string spellId)
        {
            if (Concentrating) throw new InvalidOperationException("Already concentrating.");
            ApplyChange(new ConcentrationStarted(Id, spellId));
        }

        public void EndConcentration()
        {
            if (!Concentrating) throw new InvalidOperationException("Not concentrating.");
            ApplyChange(new ConcentrationEnded(Id, ConcentratingOnSpellId ?? "", "voluntary"));
        }

        public void MoveToPosition(int targetX, int targetY, string movementType)
        {
            ApplyChange(new CharacterMovedToPosition(Id, targetX, targetY, movementType, DateTime.UtcNow));
        }
        public void Dash() => ApplyChange(new CharacterDashed(Id));
        public void Disengage() => ApplyChange(new CharacterDisengaged(Id));
        public void Hide() => ApplyChange(new CharacterHid(Id));
        public void Climb(int distanceFeet, int climbSpeedUsed)
            => ApplyChange(new CharacterClimbed(Id, distanceFeet, climbSpeedUsed));
        public void Swim(int distanceFeet, int swimSpeedUsed)
            => ApplyChange(new CharacterSwam(Id, distanceFeet, swimSpeedUsed));
        public void Fly(int distanceFeet, int flySpeedUsed)
            => ApplyChange(new CharacterFlew(Id, distanceFeet, flySpeedUsed));
        public void Burrow(int distanceFeet, int burrowSpeedUsed)
            => ApplyChange(new CharacterBurrowed(Id, distanceFeet, burrowSpeedUsed));
        public void Jump(string jumpType, int strengthScore, bool runningStart)
            => ApplyChange(new CharacterJumped(Id, jumpType, strengthScore, runningStart, 0));
        public void SetTemporarySpeed(int newSpeed, string movementType)
            => ApplyChange(new CharacterSpeedChanged(Id, newSpeed, movementType));
        public void ResetSpeedToBase() => ApplyChange(new CharacterSpeedReset(Id));
        public void ApplyDifficultTerrain(int multiplier)
            => ApplyChange(new DifficultTerrainApplied(Id, multiplier));
        public void RemoveDifficultTerrain() => ApplyChange(new DifficultTerrainRemoved(Id));
        public void ApplyMovementImpairment(string impairmentType, int speedReduction)
            => ApplyChange(new MovementImpaired(Id, impairmentType, speedReduction));
        public void RemoveMovementImpairment(string impairmentType)
            => ApplyChange(new MovementRestored(Id, impairmentType));
        public void MakeAthleticsCheck(int difficultyClass, int rollResult, int proficiencyBonus, int strengthModifier)
        {
            bool success = (rollResult + proficiencyBonus + strengthModifier) >= difficultyClass;
            ApplyChange(new AthleticsCheckForMovementMade(Id, difficultyClass, rollResult, proficiencyBonus, strengthModifier, success));
        }
        public void MakeAcrobaticsCheck(int difficultyClass, int rollResult, int proficiencyBonus, int dexterityModifier)
        {
            bool success = (rollResult + proficiencyBonus + dexterityModifier) >= difficultyClass;
            ApplyChange(new AcrobaticsCheckForMovementMade(Id, difficultyClass, rollResult, proficiencyBonus, dexterityModifier, success));
        }
        public void TakeFallDamage(int fallDistanceFeet)
        {
            int diceCount = Math.Min(fallDistanceFeet / 10, 20);
            int damage = Enumerable.Range(0, diceCount).Sum(_ => Random.Shared.Next(1, 7));
            ApplyChange(new FallDamageTaken(Id, fallDistanceFeet, damage));
            TakeDamage(damage);
        }
    }

    // Вспомогательные классы для внутренних коллекций
    public class EquippedItem
    {
        public string ItemId { get; set; } = string.Empty;
        public string Slot { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int ArmorBonus { get; set; }
        public int DamageBonus { get; set; }
    }

    public class InventoryItem
    {
        public string ItemId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }
} // конец класса