// domain/aggregates/character_aggregate.cs
using dnd_game.Domain.Events;
using dnd_game.Domain.Exceptions;
using dnd_game.Domain.Rules;
using Microsoft.Extensions.Logging;

namespace dnd_game.Domain.Aggregates
{
    /// <summary>
    /// Агрегат персонажа. Управляет всеми аспектами состояния персонажа DnD:
    /// характеристики, хиты, опыт, инвентарь, заклинания, состояния, защита, перемещение и т.д.
    /// Реализует событийно-ориентированное восстановление состояния (event sourcing).
    /// </summary>
    public class CharacterAggregate : AggregateRoot
    {
        // ---------- Основные параметры ----------

        /// <summary>Имя персонажа.</summary>
        public string Name { get; private set; } = string.Empty;

        /// <summary>Текущие хиты.</summary>
        public int HitPoints { get; private set; }

        /// <summary>Максимальные хиты.</summary>
        public int MaxHitPoints { get; private set; }

        /// <summary>Временные хиты.</summary>
        public int TemporaryHitPoints { get; private set; }

        /// <summary>Класс брони (AC).</summary>
        public int ArmorClass { get; private set; } = 10;

        /// <summary>Скорость передвижения в футах.</summary>
        public int Speed { get; private set; } = 30;

        /// <summary>Позиция по оси X на карте.</summary>
        public int PositionX { get; private set; }

        /// <summary>Позиция по оси Y на карте.</summary>
        public int PositionY { get; private set; }

        /// <summary>Накопленный опыт.</summary>
        public int ExperiencePoints { get; private set; }

        /// <summary>Текущий уровень.</summary>
        public int Level { get; private set; } = 1;

        /// <summary>Бонус мастерства.</summary>
        public int ProficiencyBonus { get; private set; } = 2;

        /// <summary>Количество золота.</summary>
        public int Gold { get; private set; }

        /// <summary>Раса персонажа.</summary>
        public string Race { get; private set; } = string.Empty;

        /// <summary>Класс персонажа.</summary>
        public string Class { get; private set; } = string.Empty;

        /// <summary>Предыстория персонажа.</summary>
        public string Background { get; private set; } = string.Empty;

        /// <summary>Текущий тип отдыха (например, "Short" или "Long"), используется для определения восстановления.</summary>
        private string _currentRestType = "";

        // ---------- Методы управления золотом ----------

        /// <summary>
        /// Добавляет золото персонажу.
        /// </summary>
        /// <param name="amount">Количество золота (должно быть положительным).</param>
        /// <exception cref="ArgumentException">Если amount меньше или равен нулю.</exception>
        /// <exception cref="InvalidOperationException">Если персонаж мёртв.</exception>
        public void AddGold(int amount)
        {
            if (amount <= 0) throw new ArgumentException("Amount must be positive.", nameof(amount));
            if (IsDead) throw new InvalidOperationException("Cannot add gold to a dead character.");
            ApplyChange(new GoldAdded(Id, amount));
        }

        /// <summary>
        /// Тратит золото персонажа (уменьшает баланс).
        /// </summary>
        /// <param name="amount">Сумма траты (должна быть положительной).</param>
        /// <exception cref="ArgumentException">Если amount меньше или равен нулю.</exception>
        /// <exception cref="InvalidOperationException">Если персонаж мёртв или золота недостаточно.</exception>
        public void SpendGold(int amount)
        {
            if (amount <= 0) throw new ArgumentException("Amount must be positive.", nameof(amount));
            if (IsDead) throw new InvalidOperationException("Cannot spend gold while dead.");
            if (Gold < amount) throw new InvalidOperationException($"Insufficient gold. Required: {amount}, available: {Gold}.");
            ApplyChange(new GoldSpent(Id, amount));
        }

        /// <summary>
        /// Устанавливает точное количество золота (для административных целей или чит-кодов).
        /// </summary>
        /// <param name="amount">Новое количество золота (не может быть отрицательным).</param>
        /// <exception cref="ArgumentException">Если amount отрицательный.</exception>
        public void SetGold(int amount)
        {
            if (amount < 0) throw new ArgumentException("Gold cannot be negative.", nameof(amount));
            ApplyChange(new GoldSet(Id, amount));
        }

        /// <summary>
        /// Начинает отдых указанного типа.
        /// </summary>
        /// <param name="restType">Тип отдыха ("Short" или "Long").</param>
        public void StartRest(string restType)
        {
            _currentRestType = restType;
            ApplyChange(new RestStarted(Id, restType, DateTime.UtcNow));
        }

        /// <summary>
        /// Прерывает текущий отдых по указанной причине.
        /// </summary>
        /// <param name="interruptionType">Тип прерывания (например, "combat").</param>
        public void InterruptRest(string interruptionType)
        {
            ApplyChange(new RestInterrupted(Id, interruptionType, DateTime.UtcNow));
        }

        /// <summary>
        /// Завершает отдых, восстанавливая хиты в зависимости от типа отдыха.
        /// </summary>
        public void EndRest()
        {
            int hpRestored = _currentRestType == "Long" ? MaxHitPoints - HitPoints : 0;
            ApplyChange(new RestCompleted(Id, _currentRestType, hpRestored, DateTime.UtcNow));
            _currentRestType = "";
        }

        /// <summary>
        /// Повышает уровень персонажа до указанного.
        /// </summary>
        /// <param name="newLevel">Новый уровень (должен быть больше текущего и не превышать 20).</param>
        /// <exception cref="ArgumentException">Если новый уровень некорректен.</exception>
        public void LevelUp(int newLevel)
        {
            if (newLevel <= Level || newLevel > 20) throw new ArgumentException("Invalid new level.");
            int newProfBonus = 2 + (int)Math.Floor((newLevel - 1) / 4.0);
            ApplyChange(new CharacterLevelUp(Id, newLevel, newProfBonus));
        }

        /// <summary>
        /// Использует ячейку заклинания указанного уровня (упрощённая реализация без проверки наличия заклинания).
        /// </summary>
        /// <param name="spellId">Идентификатор заклинания (в текущей версии не используется).</param>
        /// <param name="spellSlotLevel">Уровень ячейки заклинания.</param>
        /// <exception cref="InvalidOperationException">Если уровень ячейки отсутствует в словаре максимальных ячеек.</exception>
        public void CastSpell(string spellId, int spellSlotLevel)
        {
            if (!MaxSpellSlots.ContainsKey(spellSlotLevel))
                throw new InvalidOperationException("No such spell slot level.");
            ApplyChange(new SpellSlotUsed(Id, spellSlotLevel));
        }

        /// <summary>
        /// Выполняет короткий отдых. В упрощённой реализации только фиксирует событие, восстановление хитов не выполняется.
        /// </summary>
        /// <param name="v">Количество потраченных костей хитов (в текущей версии игнорируется).</param>
        public void TakeShortRest(int v)
        {
            ApplyChange(new RestCompleted(Id, "Short", 0, DateTime.UtcNow));
        }

        /// <summary>
        /// Выполняет длинный отдых: восстанавливает все хиты и (в перспективе) ячейки заклинаний и кости хитов.
        /// </summary>
        public void TakeLongRest()
        {
            ApplyChange(new RestCompleted(Id, "Long", MaxHitPoints - HitPoints, DateTime.UtcNow));
            // Восстановление ячеек и костей хитов обрабатывается в ApplyEvent по соответствующим событиям.
        }

        /// <summary>
        /// Совершает спасбросок по указанной характеристике. Результат проверки должен обрабатываться внешним кодом.
        /// </summary>
        /// <param name="abilityType">Название характеристики (например, "Dexterity").</param>
        /// <param name="difficultyClass">Сложность спасброска.</param>
        /// <param name="rollResult">Результат броска d20.</param>
        /// <exception cref="ArgumentException">Если характеристика не существует.</exception>
        public void MakeSavingThrow(string abilityType, int difficultyClass, int rollResult)
        {
            if (!AbilityScores.ContainsKey(abilityType)) throw new ArgumentException("Invalid ability.");
            // Здесь можно применить событие попытки спасброска, но в текущей реализации оно не создаётся.
        }

        /// <summary>
        /// Выполняет спасбросок от смерти: при результате 10+ — успех, иначе — провал.
        /// </summary>
        /// <param name="rollResult">Результат броска d20.</param>
        public void MakeDeathSavingThrow(int rollResult)
        {
            bool success = rollResult >= 10;
            if (success)
                ApplyChange(new DeathSavingThrowSuccess(Id));
            else
                ApplyChange(new DeathSavingThrowFailure(Id));
        }

        /// <summary>
        /// Устанавливает бонус мастерства вручную (обычно вычисляется автоматически при повышении уровня).
        /// </summary>
        /// <param name="bonus">Новый бонус мастерства (от 2 до 6).</param>
        /// <exception cref="ArgumentException">Если значение вне допустимого диапазона.</exception>
        public void SetProficiencyBonus(int bonus)
        {
            if (bonus < 2 || bonus > 6) throw new ArgumentException("Invalid proficiency bonus.");
            ApplyChange(new ProficiencyBonusUpdated(Id, bonus));
        }

        /// <summary>
        /// Подготавливает заклинание (если оно известно).
        /// </summary>
        /// <param name="spellId">Идентификатор заклинания.</param>
        /// <exception cref="InvalidOperationException">Если заклинание не известно персонажу.</exception>
        public void PrepareSpell(string spellId)
        {
            if (!KnownSpells.Contains(spellId)) throw new InvalidOperationException("Spell not known.");
            ApplyChange(new SpellPrepared(Id, spellId));
        }

        /// <summary>
        /// Отменяет подготовку заклинания.
        /// </summary>
        /// <param name="spellId">Идентификатор заклинания.</param>
        public void UnprepareSpell(string spellId)
        {
            ApplyChange(new SpellUnprepared(Id, spellId));
        }

        /// <summary>
        /// Использует классовое умение. Проверка доступности должна выполняться внешним кодом.
        /// </summary>
        /// <param name="featureId">Идентификатор умения.</param>
        public void UseClassFeature(string featureId)
        {
            ApplyChange(new ClassFeatureUsed(Id, featureId));
        }

        /// <summary>
        /// Восстанавливает использование классового умения (например, после отдыха).
        /// </summary>
        /// <param name="featureId">Идентификатор умения.</param>
        public void RechargeFeature(string featureId)
        {
            ApplyChange(new ClassFeatureRecharged(Id, featureId));
        }

        /// <summary>
        /// Настраивает (аттунит) магический предмет. Лимит 3 предмета должен проверяться вне агрегата.
        /// </summary>
        /// <param name="itemId">Идентификатор предмета.</param>
        public void AttuneItem(string itemId)
        {
            ApplyChange(new ItemAttuned(Id, itemId));
        }

        /// <summary>
        /// Снимает настройку с магического предмета.
        /// </summary>
        /// <param name="itemId">Идентификатор предмета.</param>
        public void UnattuneItem(string itemId)
        {
            ApplyChange(new ItemUnattuned(Id, itemId));
        }

        /// <summary>
        /// Сбрасывает счётчики спасбросков от смерти (например, после стабилизации или лечения).
        /// </summary>
        public void ResetDeathSavingThrows()
        {
            ApplyChange(new DeathSavingThrowsReset(Id));
        }

        // ---------- Характеристики ----------

        /// <summary>
        /// Словарь характеристик персонажа: название → значение (от 1 до 30).
        /// </summary>
        public Dictionary<string, int> AbilityScores { get; private set; } = new()
        {
            {"Strength", 10}, {"Dexterity", 10}, {"Constitution", 10},
            {"Intelligence", 10}, {"Wisdom", 10}, {"Charisma", 10}
        };

        // ---------- Владения ----------

        /// <summary>Список навыков, которыми владеет персонаж.</summary>
        public List<string> SkillProficiencies { get; private set; } = [];

        /// <summary>Список характеристик, для которых персонаж владеет спасбросками.</summary>
        public List<string> SavingThrowProficiencies { get; private set; } = [];

        // ---------- Черты ----------

        /// <summary>Список известных черт.</summary>
        public List<string> Feats { get; private set; } = [];

        // ---------- Заклинания ----------

        /// <summary>Список известных заклинаний (идентификаторы).</summary>
        public List<string> KnownSpells { get; private set; } = [];

        /// <summary>Словарь максимального количества ячеек заклинаний: уровень ячейки → максимум.</summary>
        public Dictionary<int, int> MaxSpellSlots { get; private set; } = [];

        /// <summary>Словарь использованных ячеек заклинаний: уровень ячейки → использовано.</summary>
        public Dictionary<int, int> UsedSpellSlots { get; private set; } = [];

        // ---------- Кости хитов ----------

        /// <summary>Словарь оставшихся костей хитов: тип кости (d6,d8,d10,d12) → количество.</summary>
        public Dictionary<int, int> HitDiceRemaining { get; private set; } = [];

        /// <summary>Словарь максимального количества костей хитов каждого типа.</summary>
        public Dictionary<int, int> MaxHitDice { get; private set; } = [];

        // ---------- Смерть и спасброски ----------

        /// <summary>Мёртв ли персонаж.</summary>
        public bool IsDead { get; private set; }

        /// <summary>Стабилизирован ли персонаж (при 0 хитов).</summary>
        public bool IsStable { get; private set; }

        /// <summary>Количество успешных спасбросков от смерти (0-3).</summary>
        public int DeathSaveSuccesses { get; private set; }

        /// <summary>Количество проваленных спасбросков от смерти (0-3).</summary>
        public int DeathSaveFailures { get; private set; }

        // ---------- Состояния ----------

        /// <summary>Список активных состояний (например, "оглушён", "ослеплён").</summary>
        public List<string> Conditions { get; private set; } = [];

        // ---------- Защиты ----------

        /// <summary>Список сопротивлений урону.</summary>
        public List<string> Resistances { get; private set; } = [];

        /// <summary>Список уязвимостей к урону.</summary>
        public List<string> Vulnerabilities { get; private set; } = [];

        /// <summary>Список иммунитетов к урону.</summary>
        public List<string> Immunities { get; private set; } = [];

        // ---------- Экипировка и инвентарь ----------

        /// <summary>Список экипированных предметов.</summary>
        public List<EquippedItem> Equipment { get; private set; } = [];

        /// <summary>Список предметов в инвентаре.</summary>
        public List<InventoryItem> Inventory { get; private set; } = [];

        // ---------- Концентрация ----------

        /// <summary>Поддерживает ли персонаж концентрацию.</summary>
        public bool Concentrating { get; private set; }

        /// <summary>Идентификатор заклинания, на котором сконцентрирован персонаж (если есть).</summary>
        public string? ConcentratingOnSpellId { get; private set; }

        // ---------- Прочее ----------

        /// <summary>Жив ли персонаж (не мёртв).</summary>
        public bool IsAlive => !IsDead;

        /// <summary>Находится ли персонаж без сознания (0 хитов, не мёртв и не стабилизирован).</summary>
        public bool IsUnconscious => HitPoints <= 0 && !IsDead && !IsStable;


        // ---------- Конструкторы ----------

        /// <summary>
        /// Создаёт нового персонажа с указанными параметрами.
        /// </summary>
        /// <param name="id">Идентификатор персонажа.</param>
        /// <param name="name">Имя персонажа.</param>
        /// <param name="maxHp">Максимальные хиты (по умолчанию 10).</param>
        public CharacterAggregate(Guid id, string name, int maxHp = 10)
        {
            ApplyChange(new CharacterCreated(id, name, maxHp, DateTime.UtcNow));
        }

        /// <summary>
        /// Конструктор без параметров для восстановления агрегата из истории событий.
        /// </summary>
        public CharacterAggregate() { }

        // ---------- Применение событий ----------

        /// <summary>
        /// Применяет доменное событие к состоянию агрегата.
        /// Обрабатывает только известные события; для ещё не реализованных событий (движение, отдых и т.д.)
        /// необходимо добавить соответствующие case-блоки.
        /// </summary>
        /// <param name="event">Событие предметной области.</param>
        protected override void ApplyEvent(IDomainEvent @event)
        {
            switch (@event)
            {
                // Создание персонажа: инициализация идентификатора, имени и хитов
                case CharacterCreated e:
                    Id = e.CharacterId;
                    Name = e.Name;
                    MaxHitPoints = e.MaxHitPoints;
                    HitPoints = e.MaxHitPoints;
                    break;

                // Обновление основных данных (имя, максимальные хиты)
                case CharacterUpdated e:
                    if (e.Name != null) Name = e.Name;
                    if (e.MaxHitPoints.HasValue)
                    {
                        MaxHitPoints = e.MaxHitPoints.Value;
                        if (HitPoints > MaxHitPoints) HitPoints = MaxHitPoints;
                    }
                    break;

                // Получение урона: сначала временные хиты, затем обычные
                case CharacterDamageTaken e:
                    ApplyDamage(e.Amount);
                    break;

                // Лечение: увеличение хитов
                case CharacterHealed e:
                    HealHitPoints(e.Amount);
                    break;

                // Установка временных хитов (сравниваем с текущими, берём большее)
                case TemporaryHitPointsSet e:
                    TemporaryHitPoints = Math.Max(TemporaryHitPoints, e.Amount);
                    break;

                // Получение опыта
                case ExperienceGained e:
                    ExperiencePoints += e.Amount;
                    break;

                // Повышение уровня
                case CharacterLevelUp e:
                    Level = e.NewLevel;
                    ProficiencyBonus = e.NewProficiencyBonus;
                    break;

                // Установка значения характеристики (ограничение 1..30)
                case AbilityScoreSet e:
                    AbilityScores[e.Ability] = Math.Clamp(e.Score, 1, 30);
                    break;

                // Выбор расы
                case RaceChosen e: Race = e.Race; break;

                // Выбор класса
                case ClassChosen e: Class = e.ClassName; break;

                // Выбор предыстории
                case BackgroundChosen e: Background = e.BackgroundName; break;

                // Добавление владения навыком
                case SkillProficiencyAdded e: if (!SkillProficiencies.Contains(e.Skill)) SkillProficiencies.Add(e.Skill); break;

                // Удаление владения навыком
                case SkillProficiencyRemoved e: SkillProficiencies.Remove(e.Skill); break;

                // Добавление владения спасброском
                case SavingThrowProficiencyAdded e: if (!SavingThrowProficiencies.Contains(e.Ability)) SavingThrowProficiencies.Add(e.Ability); break;

                // Удаление владения спасброском
                case SavingThrowProficiencyRemoved e: SavingThrowProficiencies.Remove(e.Ability); break;

                // Добавление черты
                case FeatAdded e: if (!Feats.Contains(e.FeatName)) Feats.Add(e.FeatName); break;

                // Удаление черты
                case FeatRemoved e: Feats.Remove(e.FeatName); break;

                // Добавление заклинания в известные
                case SpellAdded e: if (!KnownSpells.Contains(e.SpellId)) KnownSpells.Add(e.SpellId); break;

                // Удаление заклинания из известных
                case SpellRemoved e: KnownSpells.Remove(e.SpellId); break;

                // Установка максимальных ячеек заклинаний (и сброс использованных)
                case SpellSlotsSet e:
                    {
                        MaxSpellSlots = new Dictionary<int, int>(e.MaxSlots);
                        UsedSpellSlots = e.MaxSlots.ToDictionary(kvp => kvp.Key, _ => 0);
                        break;
                    }

                // Использование ячейки заклинания
                case SpellSlotUsed e:
                    if (UsedSpellSlots.TryGetValue(e.SlotLevel, out int value))
                        UsedSpellSlots[e.SlotLevel] = ++value;
                    break;

                // Восстановление всех ячеек заклинаний
                case SpellSlotsRestored e:
                    {
                        foreach (var slotLevel in UsedSpellSlots.Keys.ToList())
                        {
                            if (MaxSpellSlots.TryGetValue(slotLevel, out int maxSlots))
                                UsedSpellSlots[slotLevel] = 0;
                        }
                        break;
                    }

                // Установка костей хитов (максимальные и оставшиеся равны)
                case HitDiceSet e:
                    {
                        HitDiceRemaining = new Dictionary<int, int>(e.Dice);
                        MaxHitDice = new Dictionary<int, int>(e.Dice);
                        break;
                    }

                // Расходование кости хитов
                case HitDieSpent e:
                    {
                        if (HitDiceRemaining.TryGetValue(e.HitDieType, out int remaining))
                            HitDiceRemaining[e.HitDieType] = Math.Max(0, remaining - 1);
                        break;
                    }

                // Восстановление костей хитов (например, после длинного отдыха)
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

                // Наложение состояния (если ещё не наложено)
                case ConditionApplied e:
                    if (!Conditions.Contains(e.Condition)) Conditions.Add(e.Condition);
                    break;

                // Снятие состояния
                case ConditionRemoved e:
                    Conditions.Remove(e.Condition);
                    break;

                // Снятие всех состояний
                case AllConditionsCleared e:
                    Conditions.Clear();
                    break;

                // Обновление класса брони
                case ArmorClassUpdated e: ArmorClass = e.NewArmorClass; break;

                // Обновление скорости
                case SpeedUpdated e: Speed = e.NewSpeed; break;

                // Перемещение на новую позицию
                case CharacterMovedToPosition e: PositionX = e.TargetX; PositionY = e.TargetY; break;

                // Добавление сопротивления урону
                case ResistanceAdded e: if (!Resistances.Contains(e.DamageType)) Resistances.Add(e.DamageType); break;

                // Добавление уязвимости
                case VulnerabilityAdded e: if (!Vulnerabilities.Contains(e.DamageType)) Vulnerabilities.Add(e.DamageType); break;

                // Добавление иммунитета
                case ImmunityAdded e: if (!Immunities.Contains(e.DamageType)) Immunities.Add(e.DamageType); break;

                // Удаление сопротивления
                case ResistanceRemoved e: Resistances.Remove(e.DamageType); break;

                // Удаление уязвимости
                case VulnerabilityRemoved e: Vulnerabilities.Remove(e.DamageType); break;

                // Удаление иммунитета
                case ImmunityRemoved e: Immunities.Remove(e.DamageType); break;

                // Экипировка предмета (замена предмета в слоте)
                case ItemEquipped e:
                    Equipment.RemoveAll(i => i.Slot == e.Slot);
                    Equipment.Add(new EquippedItem { ItemId = e.ItemId, Slot = e.Slot, Name = e.ItemName, ArmorBonus = e.ArmorBonus, DamageBonus = e.DamageBonus });
                    break;

                // Снятие предмета
                case ItemUnequipped e:
                    Equipment.RemoveAll(i => i.ItemId == e.ItemId);
                    break;

                // Добавление предмета в инвентарь
                case InventoryItemAdded e:
                    var existing = Inventory.FirstOrDefault(i => i.ItemId == e.ItemId);
                    if (existing != null) existing.Quantity += e.Quantity;
                    else Inventory.Add(new InventoryItem { ItemId = e.ItemId, Name = e.ItemName, Quantity = e.Quantity });
                    break;

                // Удаление предмета из инвентаря
                case InventoryItemRemoved e:
                    var invItem = Inventory.FirstOrDefault(i => i.ItemId == e.ItemId);
                    if (invItem != null)
                    {
                        invItem.Quantity -= e.Quantity;
                        if (invItem.Quantity <= 0) Inventory.Remove(invItem);
                    }
                    break;

                // Успешный спасбросок от смерти
                case DeathSavingThrowSuccess e:
                    DeathSaveSuccesses = Math.Min(DeathSaveSuccesses + 1, 3);
                    if (DeathSaveSuccesses >= 3) IsStable = true;
                    break;

                // Проваленный спасбросок от смерти
                case DeathSavingThrowFailure e:
                    DeathSaveFailures = Math.Min(DeathSaveFailures + 1, 3);
                    if (DeathSaveFailures >= 3) IsDead = true;
                    break;

                // Стабилизация персонажа
                case CharacterStabilized e:
                    IsStable = true;
                    DeathSaveSuccesses = 0;
                    DeathSaveFailures = 0;
                    break;

                // Смерть персонажа
                case CharacterDied e:
                    IsDead = true;
                    break;

                // Воскрешение персонажа
                case CharacterRevived e:
                    IsDead = false;
                    HitPoints = e.NewHitPoints;
                    DeathSaveSuccesses = 0;
                    DeathSaveFailures = 0;
                    IsStable = false;
                    break;

                // Начало концентрации
                case ConcentrationStarted e:
                    Concentrating = true;
                    ConcentratingOnSpellId = e.SpellId;
                    break;

                // Окончание концентрации
                case ConcentrationEnded e:
                    Concentrating = false;
                    ConcentratingOnSpellId = null;
                    break;

                // Добавление золота
                case GoldAdded e:
                    Gold += e.Amount;
                    break;

                // Трата золота (без проверки, так как проверка была на этапе команды)
                case GoldSpent e:
                    Gold -= e.Amount;
                    break;

                // Установка точного количества золота
                case GoldSet e:
                    Gold = e.Amount;
                    break;
            }
        }

        // ---------- Приватные методы модификации (без событий) ----------

        /// <summary>
        /// Применяет урон к персонажу: сначала поглощается временными хытами, затем обычными.
        /// </summary>
        /// <param name="amount">Количество урона.</param>
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
                // Персонаж теряет сознание; дальнейшие эффекты (спасброски смерти) обрабатываются внешними правилами.
            }
        }

        /// <summary>
        /// Восстанавливает хиты персонажу (только если персонаж жив или стабилизирован).
        /// При восстановлении хитов снимает состояние "при смерти" и сбрасывает счётчики спасбросков смерти.
        /// </summary>
        /// <param name="amount">Количество восстанавливаемых хитов.</param>
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

        /// <summary>
        /// Проверяет и корректирует инварианты агрегата: хиты не выходят за допустимые границы,
        /// уровень в пределах 1..20, характеристики в пределах 1..30, использованные ячейки не превышают максимум.
        /// </summary>
        /// <exception cref="RuleViolation">Если какой-либо инвариант нарушен.</exception>
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

        /// <summary>
        /// Наносит урон персонажу.
        /// </summary>
        /// <param name="amount">Количество урона (положительное).</param>
        /// <exception cref="ArgumentException">Если amount меньше или равен нулю.</exception>
        /// <exception cref="RuleViolation">Если персонаж уже мёртв.</exception>
        public void TakeDamage(int amount)
        {
            if (amount <= 0) throw new ArgumentException("Damage must be positive");
            if (IsDead) throw new RuleViolation("Character", "Cannot damage a dead character.");
            ApplyChange(new CharacterDamageTaken(Id, amount, DateTime.UtcNow));
        }

        /// <summary>
        /// Лечит персонажа.
        /// </summary>
        /// <param name="amount">Количество восстанавливаемых хитов (положительное).</param>
        /// <exception cref="ArgumentException">Если amount меньше или равен нулю.</exception>
        /// <exception cref="RuleViolation">Если персонаж мёртв.</exception>
        public void Heal(int amount)
        {
            if (amount <= 0) throw new ArgumentException("Heal must be positive");
            if (IsDead) throw new RuleViolation("Character", "Cannot heal a dead character.");
            ApplyChange(new CharacterHealed(Id, amount, DateTime.UtcNow));
        }

        /// <summary>
        /// Обновляет основные данные персонажа (имя и/или максимальные хиты).
        /// </summary>
        /// <param name="name">Новое имя (может быть null).</param>
        /// <param name="maxHp">Новое максимальное количество хитов (может быть null).</param>
        /// <exception cref="ArgumentException">Если оба параметра null.</exception>
        /// <exception cref="RuleViolation">Если имя не проходит валидацию.</exception>
        public void Update(string? name, int? maxHp)
        {
            if (name == null && maxHp == null) throw new ArgumentException("At least one field must be provided");
            if (name != null && !ValidationRules.IsValidCharacterName(name))
                throw new RuleViolation("Validation", "Invalid character name");
            ApplyChange(new CharacterUpdated(Id, name, maxHp, DateTime.UtcNow));
        }

        /// <summary>
        /// Устанавливает временные хиты.
        /// </summary>
        /// <param name="amount">Количество временных хитов (не может быть отрицательным).</param>
        /// <exception cref="ArgumentException">Если amount отрицательный.</exception>
        public void SetTemporaryHitPoints(int amount)
        {
            if (amount < 0) throw new ArgumentException("Temporary HP cannot be negative.");
            ApplyChange(new TemporaryHitPointsSet(Id, amount));
        }

        /// <summary>
        /// Начисляет опыт персонажу.
        /// </summary>
        /// <param name="amount">Количество опыта (положительное).</param>
        /// <exception cref="ArgumentException">Если amount меньше или равен нулю.</exception>
        public void GainExperience(int amount)
        {
            if (amount <= 0) throw new ArgumentException("Experience must be positive.");
            ApplyChange(new ExperienceGained(Id, amount));
        }

        /// <summary>
        /// Устанавливает значение характеристики.
        /// </summary>
        /// <param name="ability">Название характеристики (например, "Strength").</param>
        /// <param name="score">Новое значение (будет ограничено диапазоном 1..30).</param>
        /// <exception cref="ArgumentException">Если характеристика не существует.</exception>
        public void SetAbilityScore(string ability, int score)
        {
            if (!AbilityScores.ContainsKey(ability)) throw new ArgumentException("Invalid ability.");
            ApplyChange(new AbilityScoreSet(Id, ability, score));
        }

        /// <summary>
        /// Выбирает расу персонажа.
        /// </summary>
        /// <param name="race">Название расы.</param>
        /// <exception cref="ArgumentException">Если строка пустая.</exception>
        public void ChooseRace(string race)
        {
            if (string.IsNullOrWhiteSpace(race)) throw new ArgumentException("Race cannot be empty.");
            ApplyChange(new RaceChosen(Id, race));
        }

        /// <summary>
        /// Выбирает класс персонажа.
        /// </summary>
        /// <param name="className">Название класса.</param>
        /// <exception cref="ArgumentException">Если строка пустая.</exception>
        public void ChooseClass(string className)
        {
            if (string.IsNullOrWhiteSpace(className)) throw new ArgumentException("Class cannot be empty.");
            ApplyChange(new ClassChosen(Id, className));
        }

        /// <summary>
        /// Выбирает предысторию персонажа.
        /// </summary>
        /// <param name="backgroundName">Название предыстории.</param>
        /// <exception cref="ArgumentException">Если строка пустая.</exception>
        public void ChooseBackground(string backgroundName)
        {
            if (string.IsNullOrWhiteSpace(backgroundName)) throw new ArgumentException("Background cannot be empty.");
            ApplyChange(new BackgroundChosen(Id, backgroundName));
        }

        /// <summary>
        /// Добавляет владение навыком.
        /// </summary>
        /// <param name="skill">Название навыка.</param>
        /// <exception cref="InvalidOperationException">Если персонаж уже владеет этим навыком.</exception>
        public void AddSkillProficiency(string skill)
        {
            if (SkillProficiencies.Contains(skill)) throw new InvalidOperationException("Already proficient.");
            ApplyChange(new SkillProficiencyAdded(Id, skill));
        }

        /// <summary>
        /// Удаляет владение навыком.
        /// </summary>
        /// <param name="skill">Название навыка.</param>
        /// <exception cref="InvalidOperationException">Если персонаж не владеет этим навыком.</exception>
        public void RemoveSkillProficiency(string skill)
        {
            if (!SkillProficiencies.Contains(skill)) throw new InvalidOperationException("Not proficient.");
            ApplyChange(new SkillProficiencyRemoved(Id, skill));
        }

        /// <summary>
        /// Добавляет владение спасброском.
        /// </summary>
        /// <param name="ability">Название характеристики (например, "Dexterity").</param>
        /// <exception cref="InvalidOperationException">Если персонаж уже владеет этим спасброском.</exception>
        public void AddSavingThrowProficiency(string ability)
        {
            if (SavingThrowProficiencies.Contains(ability)) throw new InvalidOperationException("Already proficient.");
            ApplyChange(new SavingThrowProficiencyAdded(Id, ability));
        }

        /// <summary>
        /// Удаляет владение спасброском.
        /// </summary>
        /// <param name="ability">Название характеристики.</param>
        /// <exception cref="InvalidOperationException">Если персонаж не владеет этим спасброском.</exception>
        public void RemoveSavingThrowProficiency(string ability)
        {
            if (!SavingThrowProficiencies.Contains(ability)) throw new InvalidOperationException("Not proficient.");
            ApplyChange(new SavingThrowProficiencyRemoved(Id, ability));
        }

        /// <summary>
        /// Добавляет черту.
        /// </summary>
        /// <param name="featName">Название черты.</param>
        /// <exception cref="InvalidOperationException">Если персонаж уже знает эту черту.</exception>
        public void AddFeat(string featName)
        {
            if (Feats.Contains(featName)) throw new InvalidOperationException("Feat already known.");
            ApplyChange(new FeatAdded(Id, featName));
        }

        /// <summary>
        /// Удаляет черту.
        /// </summary>
        /// <param name="featName">Название черты.</param>
        /// <exception cref="InvalidOperationException">Если персонаж не знает эту черту.</exception>
        public void RemoveFeat(string featName)
        {
            if (!Feats.Contains(featName)) throw new InvalidOperationException("Feat not known.");
            ApplyChange(new FeatRemoved(Id, featName));
        }

        /// <summary>
        /// Добавляет заклинание в список известных.
        /// </summary>
        /// <param name="spellId">Идентификатор заклинания.</param>
        /// <exception cref="InvalidOperationException">Если заклинание уже известно.</exception>
        public void AddSpell(string spellId)
        {
            if (KnownSpells.Contains(spellId)) throw new InvalidOperationException("Spell already known.");
            ApplyChange(new SpellAdded(Id, spellId));
        }

        /// <summary>
        /// Удаляет заклинание из списка известных.
        /// </summary>
        /// <param name="spellId">Идентификатор заклинания.</param>
        /// <exception cref="InvalidOperationException">Если заклинание не известно.</exception>
        public void RemoveSpell(string spellId)
        {
            if (!KnownSpells.Contains(spellId)) throw new InvalidOperationException("Spell not known.");
            ApplyChange(new SpellRemoved(Id, spellId));
        }

        /// <summary>
        /// Устанавливает максимальные ячейки заклинаний.
        /// </summary>
        /// <param name="maxSlots">Словарь: уровень ячейки → максимальное количество.</param>
        public void SetSpellSlots(Dictionary<int, int> maxSlots)
        {
            ApplyChange(new SpellSlotsSet(Id, maxSlots));
        }

        /// <summary>
        /// Использует одну ячейку заклинания указанного уровня.
        /// </summary>
        /// <param name="slotLevel">Уровень ячейки.</param>
        /// <exception cref="InvalidOperationException">Если уровень ячейки не существует или все ячейки уже использованы.</exception>
        public void UseSpellSlot(int slotLevel)
        {
            if (!MaxSpellSlots.TryGetValue(slotLevel, out int maxSlots))
                throw new InvalidOperationException("No such spell slot level.");
            int used = UsedSpellSlots.TryGetValue(slotLevel, out int current) ? current : 0;
            if (used >= maxSlots)
                throw new InvalidOperationException("No available spell slots of this level.");
            ApplyChange(new SpellSlotUsed(Id, slotLevel));
        }

        /// <summary>
        /// Восстанавливает все ячейки заклинаний (например, после длинного отдыха).
        /// </summary>
        public void RestoreAllSpellSlots()
        {
            foreach (var kvp in MaxSpellSlots)
                ApplyChange(new SpellSlotsRestored(Id, kvp.Key, kvp.Value));
        }

        /// <summary>
        /// Устанавливает кости хитов персонажа.
        /// </summary>
        /// <param name="dice">Словарь: тип кости (6,8,10,12) → количество.</param>
        public void SetHitDice(Dictionary<int, int> dice)
        {
            ApplyChange(new HitDiceSet(Id, dice));
        }

        /// <summary>
        /// Расходует одну кость хитов для восстановления здоровья (обычно во время короткого отдыха).
        /// </summary>
        /// <param name="hitDieType">Тип кости (например, 8 для d8).</param>
        /// <param name="roll">Результат броска кости.</param>
        /// <param name="constitutionModifier">Модификатор телосложения (добавляется к броску).</param>
        /// <exception cref="InvalidOperationException">Если кости данного типа закончились или персонаж при смерти.</exception>
        public void SpendHitDie(int hitDieType, int roll, int constitutionModifier)
        {
            if (!HitDiceRemaining.TryGetValue(hitDieType, out int value) || value <= 0)
                throw new InvalidOperationException("No hit dice of that type remaining.");
            if (HitPoints <= 0 && !IsStable) throw new InvalidOperationException("Cannot spend hit dice while dying.");
            int healed = roll + constitutionModifier;
            ApplyChange(new HitDieSpent(Id, hitDieType, healed));
        }

        /// <summary>
        /// Восстанавливает кости хитов (например, после длинного отдыха).
        /// </summary>
        /// <param name="recovered">Словарь: тип кости → сколько восстановлено.</param>
        public void RecoverHitDice(Dictionary<int, int> recovered)
        {
            ApplyChange(new HitDiceRecovered(Id, recovered));
        }

        /// <summary>
        /// Накладывает состояние на персонажа.
        /// </summary>
        /// <param name="condition">Название состояния (например, "Stunned").</param>
        /// <param name="durationRounds">Длительность в раундах (в текущей версии не используется).</param>
        /// <exception cref="ArgumentException">Если название состояния пустое.</exception>
        public void ApplyCondition(string condition, int durationRounds)
        {
            if (string.IsNullOrWhiteSpace(condition)) throw new ArgumentException("Condition cannot be empty.");
            ApplyChange(new ConditionApplied(Id, condition));
        }

        /// <summary>
        /// Снимает состояние с персонажа.
        /// </summary>
        /// <param name="condition">Название состояния.</param>
        /// <exception cref="InvalidOperationException">Если состояние не наложено.</exception>
        public void RemoveCondition(string condition)
        {
            if (!Conditions.Contains(condition)) throw new InvalidOperationException("Condition not present.");
            ApplyChange(new ConditionRemoved(Id, condition));
        }

        /// <summary>
        /// Снимает все активные состояния персонажа.
        /// </summary>
        /// <exception cref="InvalidOperationException">Если состояний нет.</exception>
        public void ClearAllConditions()
        {
            if (Conditions.Count == 0)
                throw new InvalidOperationException("No conditions to clear.");
            ApplyChange(new AllConditionsCleared(Id));
        }

        /// <summary>
        /// Обновляет класс брони.
        /// </summary>
        /// <param name="newAC">Новое значение AC (не может быть отрицательным).</param>
        /// <exception cref="ArgumentException">Если newAC отрицательный.</exception>
        public void UpdateArmorClass(int newAC)
        {
            if (newAC < 0) throw new ArgumentException("Armor class cannot be negative.");
            ApplyChange(new ArmorClassUpdated(Id, newAC));
        }

        /// <summary>
        /// Обновляет скорость передвижения.
        /// </summary>
        /// <param name="newSpeed">Новая скорость в футах (не может быть отрицательной).</param>
        /// <exception cref="ArgumentException">Если newSpeed отрицательная.</exception>
        public void UpdateSpeed(int newSpeed)
        {
            if (newSpeed < 0) throw new ArgumentException("Speed cannot be negative.");
            ApplyChange(new SpeedUpdated(Id, newSpeed));
        }

        /// <summary>
        /// Добавляет сопротивление урону.
        /// </summary>
        /// <param name="damageType">Тип урона (например, "fire").</param>
        /// <exception cref="InvalidOperationException">Если сопротивление уже есть.</exception>
        public void AddResistance(string damageType)
        {
            if (Resistances.Contains(damageType)) throw new InvalidOperationException("Already resistant.");
            ApplyChange(new ResistanceAdded(Id, damageType));
        }

        /// <summary>
        /// Удаляет сопротивление урону.
        /// </summary>
        /// <param name="damageType">Тип урона.</param>
        /// <exception cref="InvalidOperationException">Если сопротивления нет.</exception>
        public void RemoveResistance(string damageType)
        {
            if (!Resistances.Contains(damageType)) throw new InvalidOperationException("Not resistant.");
            ApplyChange(new ResistanceRemoved(Id, damageType));
        }

        /// <summary>
        /// Добавляет уязвимость к урону.
        /// </summary>
        /// <param name="damageType">Тип урона.</param>
        /// <exception cref="InvalidOperationException">Если уязвимость уже есть.</exception>
        public void AddVulnerability(string damageType)
        {
            if (Vulnerabilities.Contains(damageType)) throw new InvalidOperationException("Already vulnerable.");
            ApplyChange(new VulnerabilityAdded(Id, damageType));
        }

        /// <summary>
        /// Удаляет уязвимость.
        /// </summary>
        /// <param name="damageType">Тип урона.</param>
        /// <exception cref="InvalidOperationException">Если уязвимости нет.</exception>
        public void RemoveVulnerability(string damageType)
        {
            if (!Vulnerabilities.Contains(damageType)) throw new InvalidOperationException("Not vulnerable.");
            ApplyChange(new VulnerabilityRemoved(Id, damageType));
        }

        /// <summary>
        /// Добавляет иммунитет к урону.
        /// </summary>
        /// <param name="damageType">Тип урона.</param>
        /// <exception cref="InvalidOperationException">Если иммунитет уже есть.</exception>
        public void AddImmunity(string damageType)
        {
            if (Immunities.Contains(damageType)) throw new InvalidOperationException("Already immune.");
            ApplyChange(new ImmunityAdded(Id, damageType));
        }

        /// <summary>
        /// Удаляет иммунитет.
        /// </summary>
        /// <param name="damageType">Тип урона.</param>
        /// <exception cref="InvalidOperationException">Если иммунитета нет.</exception>
        public void RemoveImmunity(string damageType)
        {
            if (!Immunities.Contains(damageType)) throw new InvalidOperationException("Not immune.");
            ApplyChange(new ImmunityRemoved(Id, damageType));
        }

        /// <summary>
        /// Экипирует предмет в указанный слот.
        /// </summary>
        /// <param name="itemId">Идентификатор предмета.</param>
        /// <param name="slot">Слот экипировки.</param>
        /// <param name="itemName">Название предмета.</param>
        /// <param name="armorBonus">Бонус к броне (по умолчанию 0).</param>
        /// <param name="damageBonus">Бонус к урону (по умолчанию 0).</param>
        /// <exception cref="ArgumentException">Если слот пустой.</exception>
        public void EquipItem(string itemId, string slot, string itemName, int armorBonus = 0, int damageBonus = 0)
        {
            if (string.IsNullOrWhiteSpace(slot)) throw new ArgumentException("Slot required.");
            ApplyChange(new ItemEquipped(Id, itemId, slot, itemName, armorBonus, damageBonus));
        }

        /// <summary>
        /// Снимает предмет по идентификатору.
        /// </summary>
        /// <param name="itemId">Идентификатор предмета.</param>
        /// <exception cref="InvalidOperationException">Если предмет не экипирован.</exception>
        public void UnequipItem(string itemId)
        {
            if (!Equipment.Any(e => e.ItemId == itemId)) throw new InvalidOperationException("Item not equipped.");
            ApplyChange(new ItemUnequipped(Id, itemId));
        }

        /// <summary>
        /// Добавляет предмет в инвентарь.
        /// </summary>
        /// <param name="itemId">Идентификатор предмета.</param>
        /// <param name="itemName">Название предмета.</param>
        /// <param name="quantity">Количество (положительное).</param>
        /// <exception cref="ArgumentException">Если quantity меньше или равен нулю.</exception>
        public void AddInventoryItem(string itemId, string itemName, int quantity = 1)
        {
            if (quantity <= 0) throw new ArgumentException("Quantity must be positive.");
            ApplyChange(new InventoryItemAdded(Id, itemId, itemName, quantity));
        }

        /// <summary>
        /// Удаляет предмет из инвентаря.
        /// </summary>
        /// <param name="itemId">Идентификатор предмета.</param>
        /// <param name="quantity">Количество для удаления (по умолчанию 1).</param>
        /// <exception cref="InvalidOperationException">Если предмета нет или недостаточно.</exception>
        public void RemoveInventoryItem(string itemId, int quantity = 1)
        {
            var inv = Inventory.FirstOrDefault(i => i.ItemId == itemId);
            if (inv == null || inv.Quantity < quantity) throw new InvalidOperationException("Not enough items.");
            ApplyChange(new InventoryItemRemoved(Id, itemId, quantity));
        }

        /// <summary>
        /// Выполняет спасбросок от смерти (успех/провал).
        /// </summary>
        /// <param name="success">True — успех, False — провал.</param>
        /// <exception cref="InvalidOperationException">Если персонаж не находится при смерти.</exception>
        public void DeathSavingThrow(bool success)
        {
            if (HitPoints > 0 || IsDead || IsStable)
                throw new InvalidOperationException("Death saving throws only while dying.");
            if (success)
                ApplyChange(new DeathSavingThrowSuccess(Id));
            else
                ApplyChange(new DeathSavingThrowFailure(Id));
        }

        /// <summary>
        /// Стабилизирует персонажа, находящегося при смерти.
        /// </summary>
        /// <exception cref="InvalidOperationException">Если персонаж не при смерти.</exception>
        public void Stabilize()
        {
            if (HitPoints > 0 || IsDead || IsStable) throw new InvalidOperationException("Not dying.");
            ApplyChange(new CharacterStabilized(Id));
        }

        /// <summary>
        /// Помечает персонажа как мёртвого.
        /// </summary>
        /// <exception cref="InvalidOperationException">Если персонаж уже мёртв.</exception>
        public void MarkDead()
        {
            if (IsDead) throw new InvalidOperationException("Already dead.");
            ApplyChange(new CharacterDied(Id, DateTime.UtcNow));
        }

        /// <summary>
        /// Воскрешает персонажа.
        /// </summary>
        /// <param name="newHitPoints">Хиты после воскрешения (положительные).</param>
        /// <exception cref="InvalidOperationException">Если персонаж не мёртв.</exception>
        /// <exception cref="ArgumentException">Если newHitPoints меньше или равен нулю.</exception>
        public void Revive(int newHitPoints)
        {
            if (!IsDead) throw new InvalidOperationException("Character is not dead.");
            if (newHitPoints <= 0) throw new ArgumentException("Must have positive HP after revive.");
            ApplyChange(new CharacterRevived(Id, newHitPoints));
        }

        /// <summary>
        /// Начинает концентрацию на заклинании.
        /// </summary>
        /// <param name="spellId">Идентификатор заклинания.</param>
        /// <exception cref="InvalidOperationException">Если персонаж уже концентрируется.</exception>
        public void StartConcentration(string spellId)
        {
            if (Concentrating) throw new InvalidOperationException("Already concentrating.");
            ApplyChange(new ConcentrationStarted(Id, spellId));
        }

        /// <summary>
        /// Завершает концентрацию (добровольно).
        /// </summary>
        /// <exception cref="InvalidOperationException">Если персонаж не концентрируется.</exception>
        public void EndConcentration()
        {
            if (!Concentrating) throw new InvalidOperationException("Not concentrating.");
            ApplyChange(new ConcentrationEnded(Id, ConcentratingOnSpellId ?? "", "voluntary"));
        }

        /// <summary>
        /// Перемещает персонажа на указанную позицию.
        /// </summary>
        /// <param name="targetX">Целевая координата X.</param>
        /// <param name="targetY">Целевая координата Y.</param>
        /// <param name="movementType">Тип перемещения (например, "Walk", "Fly").</param>
        public void MoveToPosition(int targetX, int targetY, string movementType)
        {
            ApplyChange(new CharacterMovedToPosition(Id, targetX, targetY, movementType, DateTime.UtcNow));
        }

        /// <summary>Применяет действие Dash (рывок).</summary>
        public void Dash() => ApplyChange(new CharacterDashed(Id));

        /// <summary>Применяет действие Disengage (отход без провоцированных атак).</summary>
        public void Disengage() => ApplyChange(new CharacterDisengaged(Id));

        /// <summary>Применяет действие Hide (скрытие).</summary>
        public void Hide() => ApplyChange(new CharacterHid(Id));

        /// <summary>Выполняет лазание.</summary>
        /// <param name="distanceFeet">Дистанция в футах.</param>
        /// <param name="climbSpeedUsed">Использованная скорость лазания.</param>
        public void Climb(int distanceFeet, int climbSpeedUsed)
            => ApplyChange(new CharacterClimbed(Id, distanceFeet, climbSpeedUsed));

        /// <summary>Выполняет плавание.</summary>
        /// <param name="distanceFeet">Дистанция в футах.</param>
        /// <param name="swimSpeedUsed">Использованная скорость плавания.</param>
        public void Swim(int distanceFeet, int swimSpeedUsed)
            => ApplyChange(new CharacterSwam(Id, distanceFeet, swimSpeedUsed));

        /// <summary>Выполняет полёт.</summary>
        /// <param name="distanceFeet">Дистанция в футах.</param>
        /// <param name="flySpeedUsed">Использованная скорость полёта.</param>
        public void Fly(int distanceFeet, int flySpeedUsed)
            => ApplyChange(new CharacterFlew(Id, distanceFeet, flySpeedUsed));

        /// <summary>Выполняет копание (burrow).</summary>
        /// <param name="distanceFeet">Дистанция в футах.</param>
        /// <param name="burrowSpeedUsed">Использованная скорость копания.</param>
        public void Burrow(int distanceFeet, int burrowSpeedUsed)
            => ApplyChange(new CharacterBurrowed(Id, distanceFeet, burrowSpeedUsed));

        /// <summary>Выполняет прыжок.</summary>
        /// <param name="jumpType">Тип прыжка ("Long" или "High").</param>
        /// <param name="strengthScore">Значение силы.</param>
        /// <param name="runningStart">Был ли разбег.</param>
        public void Jump(string jumpType, int strengthScore, bool runningStart)
            => ApplyChange(new CharacterJumped(Id, jumpType, strengthScore, runningStart, 0));

        /// <summary>Устанавливает временную скорость (например, от заклинания).</summary>
        /// <param name="newSpeed">Новая скорость.</param>
        /// <param name="movementType">Тип движения, к которому применяется.</param>
        public void SetTemporarySpeed(int newSpeed, string movementType)
            => ApplyChange(new CharacterSpeedChanged(Id, newSpeed, movementType));

        /// <summary>Сбрасывает скорость до базовой.</summary>
        public void ResetSpeedToBase() => ApplyChange(new CharacterSpeedReset(Id));

        /// <summary>Применяет модификатор труднопроходимой местности.</summary>
        /// <param name="multiplier">Множитель стоимости движения.</param>
        public void ApplyDifficultTerrain(int multiplier)
            => ApplyChange(new DifficultTerrainApplied(Id, multiplier));

        /// <summary>Снимает эффект труднопроходимой местности.</summary>
        public void RemoveDifficultTerrain() => ApplyChange(new DifficultTerrainRemoved(Id));

        /// <summary>Накладывает ограничение движения.</summary>
        /// <param name="impairmentType">Тип ограничения.</param>
        /// <param name="speedReduction">Снижение скорости.</param>
        public void ApplyMovementImpairment(string impairmentType, int speedReduction)
            => ApplyChange(new MovementImpaired(Id, impairmentType, speedReduction));

        /// <summary>Снимает ограничение движения.</summary>
        /// <param name="impairmentType">Тип ограничения.</param>
        public void RemoveMovementImpairment(string impairmentType)
            => ApplyChange(new MovementRestored(Id, impairmentType));

        /// <summary>Выполняет проверку Атлетики для движения.</summary>
        /// <param name="difficultyClass">Сложность.</param>
        /// <param name="rollResult">Результат броска.</param>
        /// <param name="proficiencyBonus">Бонус мастерства.</param>
        /// <param name="strengthModifier">Модификатор силы.</param>
        public void MakeAthleticsCheck(int difficultyClass, int rollResult, int proficiencyBonus, int strengthModifier)
        {
            bool success = (rollResult + proficiencyBonus + strengthModifier) >= difficultyClass;
            ApplyChange(new AthleticsCheckForMovementMade(Id, difficultyClass, rollResult, proficiencyBonus, strengthModifier, success));
        }

        /// <summary>Выполняет проверку Акробатики для движения.</summary>
        /// <param name="difficultyClass">Сложность.</param>
        /// <param name="rollResult">Результат броска.</param>
        /// <param name="proficiencyBonus">Бонус мастерства.</param>
        /// <param name="dexterityModifier">Модификатор ловкости.</param>
        public void MakeAcrobaticsCheck(int difficultyClass, int rollResult, int proficiencyBonus, int dexterityModifier)
        {
            bool success = (rollResult + proficiencyBonus + dexterityModifier) >= difficultyClass;
            ApplyChange(new AcrobaticsCheckForMovementMade(Id, difficultyClass, rollResult, proficiencyBonus, dexterityModifier, success));
        }

        /// <summary>
        /// Применяет урон от падения и сразу наносит его.
        /// </summary>
        /// <param name="fallDistanceFeet">Высота падения в футах.</param>
        public void TakeFallDamage(int fallDistanceFeet)
        {
            int diceCount = Math.Min(fallDistanceFeet / 10, 20);
            int damage = Enumerable.Range(0, diceCount).Sum(_ => Random.Shared.Next(1, 7));
            ApplyChange(new FallDamageTaken(Id, fallDistanceFeet, damage));
            TakeDamage(damage);
        }
    }

    /// <summary>
    /// Модель экипированного предмета (внутреннее представление в агрегате).
    /// </summary>
    public class EquippedItem
    {
        /// <summary>Идентификатор предмета.</summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>Слот экипировки.</summary>
        public string Slot { get; set; } = string.Empty;

        /// <summary>Название предмета.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Бонус к броне.</summary>
        public int ArmorBonus { get; set; }

        /// <summary>Бонус к урону.</summary>
        public int DamageBonus { get; set; }
    }

    /// <summary>
    /// Модель предмета в инвентаре (внутреннее представление).
    /// </summary>
    public class InventoryItem
    {
        /// <summary>Идентификатор предмета.</summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>Название предмета.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Количество.</summary>
        public int Quantity { get; set; }
    }
}