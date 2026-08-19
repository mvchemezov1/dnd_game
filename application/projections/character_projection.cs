// application/projections/character_projection.cs
using dnd_game.Domain.Events;
using dnd_game.Domain.Queries;
using dnd_game.Infrastructure.Caching;
using dnd_game.Infrastructure.EventStore;
using System.Collections.Concurrent;

namespace dnd_game.Application.Projections;

/// <summary>
/// Проекция персонажа, отвечающая за построение read-модели на основе событий предметной области.
/// Хранит актуальное состояние персонажей в памяти (в реальном приложении — в БД)
/// и предоставляет методы чтения с кешированием.
/// </summary>
/// <remarks>
/// Инициализирует новый экземпляр проекции персонажа.
/// </remarks>
/// <param name="cache">Провайдер кеша.</param>
/// <param name="cacheTtl">Время жизни кеша; по умолчанию 5 минут.</param>
public class CharacterProjection(ICacheProvider cache, TimeSpan? cacheTtl = null)
{
    /// <summary>Хранилище DTO персонажей: идентификатор → состояние.</summary>
    private readonly ConcurrentDictionary<Guid, CharacterDto> _state = new();

    /// <summary>Провайдер кеша для ускорения повторных чтений.</summary>
    private readonly ICacheProvider _cache = cache;

    /// <summary>Время жизни записей в кеше.</summary>
    private readonly TimeSpan _cacheTtl = cacheTtl ?? TimeSpan.FromMinutes(5);

    /// <summary>
    /// Инвалидирует кеш, связанный с конкретным персонажем и общим списком персонажей.
    /// Выполняется асинхронно в фоновом потоке, чтобы не блокировать обработку события.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа, кеш которого требуется сбросить.</param>
    private void InvalidateCache(Guid characterId)
    {
        _ = Task.Run(async () =>
        {
            await _cache.RemoveAsync($"character:{characterId}");
            await _cache.RemoveAsync("characters:all");
        });
    }

    // ---------- Базовые события создания и обновления ----------

    /// <summary>
    /// Обрабатывает событие создания персонажа (<see cref="CharacterCreated"/>).
    /// Инициализирует DTO персонажа с значениями по умолчанию и сбрасывает кеш.
    /// </summary>
    public void Apply(CharacterCreated e)
    {
        _state[e.CharacterId] = new CharacterDto(
            Id: e.CharacterId,
            Name: e.Name,
            MaxHitPoints: e.MaxHitPoints,
            HitPoints: e.MaxHitPoints,
            AbilityScores: [],
            SkillProficiencies: new Dictionary<string, bool>(),
            SavingThrowProficiencies: new Dictionary<string, bool>(),
            KnownSpells: [],
            SpellSlots: [],
            UsedSpellSlots: [],
            HitDice: [],
            Conditions: [],
            Resistances: [],
            Vulnerabilities: [],
            Immunities: [],
            Equipment: [],
            Inventory: [],
            Feats: []
        );
        InvalidateCache(e.CharacterId);
    }

    /// <summary>
    /// Обрабатывает событие обновления персонажа (<see cref="CharacterUpdated"/>).
    /// Обновляет имя, максимальные хиты и при необходимости корректирует текущие хиты.
    /// </summary>
    public void Apply(CharacterUpdated e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with
            {
                Name = e.Name ?? dto.Name,
                MaxHitPoints = e.MaxHitPoints ?? dto.MaxHitPoints,
                HitPoints = Math.Min(dto.HitPoints, e.MaxHitPoints ?? dto.MaxHitPoints)
            };
            InvalidateCache(e.CharacterId);
        }
    }

    // ---------- Хиты и временные хиты ----------

    /// <summary>
    /// Обрабатывает событие получения урона (<see cref="CharacterDamageTaken"/>).
    /// Сначала урон поглощается временными хытами, затем применяется к обычным хытам.
    /// </summary>
    public void Apply(CharacterDamageTaken e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            int remainingDamage = e.Amount;
            int newTemp = dto.TemporaryHitPoints;

            if (newTemp > 0)
            {
                int absorbed = Math.Min(newTemp, remainingDamage);
                newTemp -= absorbed;
                remainingDamage -= absorbed;
            }

            int newHp = Math.Max(0, dto.HitPoints - remainingDamage);

            _state[e.CharacterId] = dto with
            {
                TemporaryHitPoints = newTemp,
                HitPoints = newHp
            };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие лечения (<see cref="CharacterHealed"/>).
    /// Увеличивает текущие хиты, не превышая максимум.
    /// </summary>
    public void Apply(CharacterHealed e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with
            {
                HitPoints = Math.Min(dto.HitPoints + e.Amount, dto.MaxHitPoints)
            };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие установки временных хитов (<see cref="TemporaryHitPointsSet"/>).
    /// Новое значение временных хитов не может быть меньше уже имеющегося (по правилам DnD).
    /// </summary>
    public void Apply(TemporaryHitPointsSet e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            int newTemp = Math.Max(dto.TemporaryHitPoints, e.Amount);
            _state[e.CharacterId] = dto with { TemporaryHitPoints = newTemp };
            InvalidateCache(e.CharacterId);
        }
    }

    // ---------- Уровни и опыт ----------

    /// <summary>
    /// Обрабатывает событие получения опыта (<see cref="ExperienceGained"/>).
    /// Увеличивает накопленный опыт.
    /// </summary>
    public void Apply(ExperienceGained e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { ExperiencePoints = dto.ExperiencePoints + e.Amount };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие повышения уровня (<see cref="CharacterLevelUp"/>).
    /// Обновляет уровень и бонус мастерства.
    /// </summary>
    public void Apply(CharacterLevelUp e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with
            {
                Level = e.NewLevel,
                ProficiencyBonus = e.NewProficiencyBonus
            };
            InvalidateCache(e.CharacterId);
        }
    }

    // ---------- Характеристики ----------

    /// <summary>
    /// Обрабатывает событие установки значения характеристики (<see cref="AbilityScoreSet"/>).
    /// Обновляет значение указанной характеристики.
    /// </summary>
    public void Apply(AbilityScoreSet e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var scores = new Dictionary<string, int>(dto.AbilityScores) { [e.Ability] = e.Score };
            _state[e.CharacterId] = dto with { AbilityScores = scores };
            InvalidateCache(e.CharacterId);
        }
    }

    // ---------- Владения навыками и спасбросками ----------

    /// <summary>
    /// Обрабатывает событие добавления владения навыком (<see cref="SkillProficiencyAdded"/>).
    /// </summary>
    public void Apply(SkillProficiencyAdded e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var skills = new Dictionary<string, bool>((IDictionary<string, bool>)dto.SkillProficiencies) { [e.Skill] = true };
            _state[e.CharacterId] = dto with { SkillProficiencies = skills };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие удаления владения навыком (<see cref="SkillProficiencyRemoved"/>).
    /// </summary>
    public void Apply(SkillProficiencyRemoved e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var skills = new Dictionary<string, bool>((IDictionary<string, bool>)dto.SkillProficiencies);
            skills.Remove(e.Skill);
            _state[e.CharacterId] = dto with { SkillProficiencies = skills };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие добавления владения спасброском (<see cref="SavingThrowProficiencyAdded"/>).
    /// </summary>
    public void Apply(SavingThrowProficiencyAdded e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var saves = new Dictionary<string, bool>((IDictionary<string, bool>)dto.SavingThrowProficiencies) { [e.Ability] = true };
            _state[e.CharacterId] = dto with { SavingThrowProficiencies = saves };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие удаления владения спасброском (<see cref="SavingThrowProficiencyRemoved"/>).
    /// </summary>
    public void Apply(SavingThrowProficiencyRemoved e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var saves = new Dictionary<string, bool>((IDictionary<string, bool>)dto.SavingThrowProficiencies);
            saves.Remove(e.Ability);
            _state[e.CharacterId] = dto with { SavingThrowProficiencies = saves };
            InvalidateCache(e.CharacterId);
        }
    }

    // ---------- Раса, класс, предыстория ----------

    /// <summary>
    /// Обрабатывает событие выбора расы (<see cref="RaceChosen"/>).
    /// </summary>
    public void Apply(RaceChosen e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { Race = e.Race };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие выбора класса (<see cref="ClassChosen"/>).
    /// </summary>
    public void Apply(ClassChosen e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { Class = e.ClassName };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие выбора предыстории (<see cref="BackgroundChosen"/>).
    /// </summary>
    public void Apply(BackgroundChosen e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { Background = e.BackgroundName };
            InvalidateCache(e.CharacterId);
        }
    }

    // ---------- Черты ----------

    /// <summary>
    /// Обрабатывает событие добавления черты (<see cref="FeatAdded"/>).
    /// </summary>
    public void Apply(FeatAdded e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var feats = new List<string>(dto.Feats) { e.FeatName };
            _state[e.CharacterId] = dto with { Feats = feats };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие удаления черты (<see cref="FeatRemoved"/>).
    /// </summary>
    public void Apply(FeatRemoved e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var feats = new List<string>(dto.Feats);
            feats.Remove(e.FeatName);
            _state[e.CharacterId] = dto with { Feats = feats };
            InvalidateCache(e.CharacterId);
        }
    }

    // ---------- Заклинания и ячейки ----------

    /// <summary>
    /// Обрабатывает событие добавления заклинания (<see cref="SpellAdded"/>).
    /// </summary>
    public void Apply(SpellAdded e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var spells = new List<string>(dto.KnownSpells) { e.SpellId };
            _state[e.CharacterId] = dto with { KnownSpells = spells };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие удаления заклинания (<see cref="SpellRemoved"/>).
    /// </summary>
    public void Apply(SpellRemoved e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var spells = new List<string>(dto.KnownSpells);
            spells.Remove(e.SpellId);
            _state[e.CharacterId] = dto with { KnownSpells = spells };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие использования ячейки заклинания (<see cref="SpellSlotUsed"/>).
    /// Увеличивает счётчик использованных ячеек указанного уровня.
    /// </summary>
    public void Apply(SpellSlotUsed e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var used = new Dictionary<int, int>(dto.UsedSpellSlots);
            used[e.SlotLevel] = used.GetValueOrDefault(e.SlotLevel, 0) + 1;
            _state[e.CharacterId] = dto with { UsedSpellSlots = used };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие восстановления всех ячеек заклинаний (<see cref="SpellSlotsRestored"/>).
    /// Сбрасывает счётчик использованных ячеек.
    /// </summary>
    public void Apply(SpellSlotsRestored e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { UsedSpellSlots = [] };
            InvalidateCache(e.CharacterId);
        }
    }

    // ---------- Состояния ----------

    /// <summary>
    /// Обрабатывает событие наложения состояния (<see cref="ConditionApplied"/>).
    /// Добавляет состояние, если его ещё нет в списке.
    /// </summary>
    public void Apply(ConditionApplied e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var conditions = new List<string>(dto.Conditions);
            if (!conditions.Contains(e.Condition))
                conditions.Add(e.Condition);
            _state[e.CharacterId] = dto with { Conditions = conditions };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие очистки всех состояний (<see cref="AllConditionsCleared"/>).
    /// </summary>
    public void Apply(AllConditionsCleared e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { Conditions = [] };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие снятия состояния (<see cref="ConditionRemoved"/>).
    /// </summary>
    public void Apply(ConditionRemoved e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var conditions = new List<string>(dto.Conditions);
            conditions.Remove(e.Condition);
            _state[e.CharacterId] = dto with { Conditions = conditions };
            InvalidateCache(e.CharacterId);
        }
    }

    // ---------- Защита и скорость ----------

    /// <summary>
    /// Обрабатывает событие обновления класса брони (<see cref="ArmorClassUpdated"/>).
    /// </summary>
    public void Apply(ArmorClassUpdated e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { ArmorClass = e.NewArmorClass };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие обновления скорости (<see cref="SpeedUpdated"/>).
    /// </summary>
    public void Apply(SpeedUpdated e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { Speed = e.NewSpeed };
            InvalidateCache(e.CharacterId);
        }
    }

    // ---------- Сопротивления, уязвимости, иммунитеты ----------

    /// <summary>
    /// Обрабатывает событие добавления сопротивления урону (<see cref="ResistanceAdded"/>).
    /// </summary>
    public void Apply(ResistanceAdded e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var res = new List<string>(dto.Resistances) { e.DamageType };
            _state[e.CharacterId] = dto with { Resistances = res };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие добавления уязвимости (<see cref="VulnerabilityAdded"/>).
    /// </summary>
    public void Apply(VulnerabilityAdded e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var vul = new List<string>(dto.Vulnerabilities) { e.DamageType };
            _state[e.CharacterId] = dto with { Vulnerabilities = vul };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие добавления иммунитета (<see cref="ImmunityAdded"/>).
    /// </summary>
    public void Apply(ImmunityAdded e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var imm = new List<string>(dto.Immunities) { e.DamageType };
            _state[e.CharacterId] = dto with { Immunities = imm };
            InvalidateCache(e.CharacterId);
        }
    }

    // ---------- Смерть и спасброски ----------

    /// <summary>
    /// Обрабатывает событие успешного спасброска от смерти (<see cref="DeathSavingThrowSuccess"/>).
    /// Увеличивает счётчик успехов (максимум 3). При достижении 3 персонаж стабилизируется.
    /// </summary>
    public void Apply(DeathSavingThrowSuccess e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            int successes = Math.Min(dto.DeathSaveSuccesses + 1, 3);
            bool stable = successes >= 3;
            _state[e.CharacterId] = dto with
            {
                DeathSaveSuccesses = successes,
                IsStable = stable
            };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие проваленного спасброска от смерти (<see cref="DeathSavingThrowFailure"/>).
    /// Увеличивает счётчик провалов (максимум 3). При достижении 3 персонаж умирает.
    /// </summary>
    public void Apply(DeathSavingThrowFailure e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            int failures = Math.Min(dto.DeathSaveFailures + 1, 3);
            bool dead = failures >= 3;
            _state[e.CharacterId] = dto with
            {
                DeathSaveFailures = failures,
                IsDead = dead
            };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие стабилизации персонажа (<see cref="CharacterStabilized"/>).
    /// Сбрасывает счётчики спасбросков от смерти и устанавливает признак стабилизации.
    /// </summary>
    public void Apply(CharacterStabilized e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with
            {
                IsStable = true,
                DeathSaveSuccesses = 0,
                DeathSaveFailures = 0
            };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие смерти персонажа (<see cref="CharacterDied"/>).
    /// </summary>
    public void Apply(CharacterDied e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { IsDead = true };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие воскрешения персонажа (<see cref="CharacterRevived"/>).
    /// Сбрасывает состояние смерти и устанавливает новые хиты.
    /// </summary>
    public void Apply(CharacterRevived e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with
            {
                IsDead = false,
                HitPoints = e.NewHitPoints,
                DeathSaveSuccesses = 0,
                DeathSaveFailures = 0,
                IsStable = false
            };
            InvalidateCache(e.CharacterId);
        }
    }

    // ---------- Экипировка и инвентарь ----------

    /// <summary>
    /// Обрабатывает событие экипировки предмета (<see cref="ItemEquipped"/>).
    /// Заменяет предмет в указанном слоте новым.
    /// </summary>
    public void Apply(ItemEquipped e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var equipment = new List<EquippedItemDto>(dto.Equipment);
            equipment.RemoveAll(i => i.Slot == e.Slot);
            equipment.Add(new EquippedItemDto(e.ItemId, e.Slot, e.ItemName, e.ArmorBonus, e.DamageBonus));
            _state[e.CharacterId] = dto with { Equipment = equipment };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие снятия предмета (<see cref="ItemUnequipped"/>).
    /// Удаляет предмет из списка экипировки по идентификатору.
    /// </summary>
    public void Apply(ItemUnequipped e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var equipment = new List<EquippedItemDto>(dto.Equipment);
            equipment.RemoveAll(i => i.ItemId == e.ItemId);
            _state[e.CharacterId] = dto with { Equipment = equipment };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие добавления предмета в инвентарь (<see cref="InventoryItemAdded"/>).
    /// Если предмет уже есть, увеличивает количество.
    /// </summary>
    public void Apply(InventoryItemAdded e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var inventory = new List<InventoryItemDto>(dto.Inventory);
            var existing = inventory.FirstOrDefault(i => i.ItemId == e.ItemId);
            if (existing != null)
                inventory.Remove(existing);
            inventory.Add(new InventoryItemDto(e.ItemId, e.ItemName, existing?.Quantity + e.Quantity ?? e.Quantity));
            _state[e.CharacterId] = dto with { Inventory = inventory };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие удаления предмета из инвентаря (<see cref="InventoryItemRemoved"/>).
    /// Уменьшает количество или удаляет предмет полностью.
    /// </summary>
    public void Apply(InventoryItemRemoved e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var inventory = new List<InventoryItemDto>(dto.Inventory);
            var existing = inventory.FirstOrDefault(i => i.ItemId == e.ItemId);
            if (existing != null)
            {
                inventory.Remove(existing);
                if (existing.Quantity > 1)
                    inventory.Add(existing with { Quantity = existing.Quantity - 1 });
            }
            _state[e.CharacterId] = dto with { Inventory = inventory };
            InvalidateCache(e.CharacterId);
        }
    }

    //---- Управление золотом --------

    /// <summary>
    /// Обрабатывает событие добавления золота (<see cref="GoldAdded"/>).
    /// </summary>
    public void Apply(GoldAdded e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { Gold = dto.Gold + e.Amount };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие траты золота (<see cref="GoldSpent"/>).
    /// Уменьшает золото, не допуская отрицательного значения.
    /// </summary>
    public void Apply(GoldSpent e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { Gold = Math.Max(0, dto.Gold - e.Amount) };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие установки точного количества золота (<see cref="GoldSet"/>).
    /// </summary>
    public void Apply(GoldSet e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { Gold = e.Amount };
            InvalidateCache(e.CharacterId);
        }
    }

    // ---------- Кости хитов и отдых ----------

    /// <summary>
    /// Обрабатывает событие расходования кости хитов (<see cref="HitDieSpent"/>).
    /// Уменьшает количество доступных костей указанного типа.
    /// </summary>
    public void Apply(HitDieSpent e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var dice = new Dictionary<int, int>(dto.HitDice);
            if (dice.TryGetValue(e.HitDieType, out int value))
                dice[e.HitDieType] = Math.Max(0, value - 1);
            _state[e.CharacterId] = dto with { HitDice = dice };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие восстановления костей хитов (<see cref="HitDiceRecovered"/>).
    /// Увеличивает количество костей, не превышая максимум.
    /// </summary>
    public void Apply(HitDiceRecovered e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var dice = new Dictionary<int, int>(dto.HitDice);
            foreach (var kv in e.Recovered)
            {
                if (dice.TryGetValue(kv.Key, out int value))
                    dice[kv.Key] = Math.Min(dto.MaxHitDice.GetValueOrDefault(kv.Key), value + kv.Value);
                else
                    dice[kv.Key] = kv.Value;
            }
            _state[e.CharacterId] = dto with { HitDice = dice };
            InvalidateCache(e.CharacterId);
        }
    }

    // ---------- Концентрация ----------

    /// <summary>
    /// Обрабатывает событие начала концентрации (<see cref="ConcentrationStarted"/>).
    /// </summary>
    public void Apply(ConcentrationStarted e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { Concentrating = true };
            InvalidateCache(e.CharacterId);
        }
    }

    /// <summary>
    /// Обрабатывает событие окончания концентрации (<see cref="ConcentrationEnded"/>).
    /// </summary>
    public void Apply(ConcentrationEnded e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { Concentrating = false };
            InvalidateCache(e.CharacterId);
        }
    }

    // ---------- Методы доступа (с кешем) ----------

    /// <summary>
    /// Получает DTO персонажа по идентификатору. Использует кеширование.
    /// </summary>
    /// <param name="id">Идентификатор персонажа.</param>
    /// <returns>Объект <see cref="CharacterDto"/> или null, если персонаж не найден.</returns>
    public async Task<CharacterDto?> GetById(Guid id)
    {
        var cacheKey = $"character:{id}";
        var cached = await _cache.GetAsync<CharacterDto>(cacheKey);
        if (cached != null)
            return cached;

        if (_state.TryGetValue(id, out var dto))
        {
            await _cache.SetAsync(cacheKey, dto, _cacheTtl);
            return dto;
        }
        return null;
    }

    /// <summary>
    /// Получает список всех персонажей. Использует кеширование.
    /// </summary>
    /// <returns>Список DTO персонажей.</returns>
    public async Task<List<CharacterDto>> GetAll()
    {
        const string cacheKey = "characters:all";
        var cached = await _cache.GetAsync<List<CharacterDto>>(cacheKey);
        if (cached != null)
            return cached;

        var list = _state.Values.ToList();
        await _cache.SetAsync(cacheKey, list, _cacheTtl);
        return list;
    }

    // ---------- Восстановление проекции из хранилища событий ----------

    /// <summary>
    /// Полностью пересобирает проекцию из всех событий, хранящихся в event store.
    /// Очищает текущее состояние и последовательно применяет события.
    /// </summary>
    /// <param name="eventStore">Хранилище событий, содержащее все события предметной области.</param>
    public async Task RebuildAsync(IEventStore eventStore)
    {
        _state.Clear();
        var allEvents = await eventStore.GetAllEvents();
        foreach (var e in allEvents)
        {
            switch (e)
            {
                case CharacterCreated ev: Apply(ev); break;
                case CharacterUpdated ev: Apply(ev); break;
                case CharacterDamageTaken ev: Apply(ev); break;
                case CharacterHealed ev: Apply(ev); break;
                case TemporaryHitPointsSet ev: Apply(ev); break;
                case ExperienceGained ev: Apply(ev); break;
                case CharacterLevelUp ev: Apply(ev); break;
                case AbilityScoreSet ev: Apply(ev); break;
                case SkillProficiencyAdded ev: Apply(ev); break;
                case SkillProficiencyRemoved ev: Apply(ev); break;
                case SavingThrowProficiencyAdded ev: Apply(ev); break;
                case SavingThrowProficiencyRemoved ev: Apply(ev); break;
                case RaceChosen ev: Apply(ev); break;
                case ClassChosen ev: Apply(ev); break;
                case BackgroundChosen ev: Apply(ev); break;
                case FeatAdded ev: Apply(ev); break;
                case FeatRemoved ev: Apply(ev); break;
                case SpellAdded ev: Apply(ev); break;
                case SpellRemoved ev: Apply(ev); break;
                case SpellSlotUsed ev: Apply(ev); break;
                case SpellSlotsRestored ev: Apply(ev); break;
                case ConditionApplied ev: Apply(ev); break;
                case ConditionRemoved ev: Apply(ev); break;
                case ArmorClassUpdated ev: Apply(ev); break;
                case SpeedUpdated ev: Apply(ev); break;
                case ResistanceAdded ev: Apply(ev); break;
                case VulnerabilityAdded ev: Apply(ev); break;
                case ImmunityAdded ev: Apply(ev); break;
                case ResistanceRemoved ev: Apply(ev); break;
                case VulnerabilityRemoved ev: Apply(ev); break;
                case ImmunityRemoved ev: Apply(ev); break;
                case DeathSavingThrowSuccess ev: Apply(ev); break;
                case DeathSavingThrowFailure ev: Apply(ev); break;
                case CharacterStabilized ev: Apply(ev); break;
                case CharacterDied ev: Apply(ev); break;
                case CharacterRevived ev: Apply(ev); break;
                case ItemEquipped ev: Apply(ev); break;
                case ItemUnequipped ev: Apply(ev); break;
                case InventoryItemAdded ev: Apply(ev); break;
                case InventoryItemRemoved ev: Apply(ev); break;
                case HitDieSpent ev: Apply(ev); break;
                case HitDiceRecovered ev: Apply(ev); break;
                case ConcentrationStarted ev: Apply(ev); break;
                case ConcentrationEnded ev: Apply(ev); break;
                case GoldAdded ev: Apply(ev); break;
                case GoldSpent ev: Apply(ev); break;
                case GoldSet ev: Apply(ev); break;
                case AllConditionsCleared ev: Apply(ev); break;
            }
        }
    }

    /// <summary>
    /// Обрабатывает событие удаления уязвимости (<see cref="VulnerabilityRemoved"/>).
    /// Внутренний метод, вызывается при восстановлении проекции или в ответ на событие.
    /// </summary>
    internal void Apply(VulnerabilityRemoved e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var vul = dto.Vulnerabilities.Where(v => v != e.DamageType).ToList();
            _state[e.CharacterId] = dto with { Vulnerabilities = vul };
        }
    }

    /// <summary>
    /// Обрабатывает событие удаления иммунитета (<see cref="ImmunityRemoved"/>).
    /// Внутренний метод, вызывается при восстановлении проекции или в ответ на событие.
    /// </summary>
    internal void Apply(ImmunityRemoved e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var imm = dto.Immunities.Where(i => i != e.DamageType).ToList();
            _state[e.CharacterId] = dto with { Immunities = imm };
        }
    }

    /// <summary>
    /// Обрабатывает событие удаления сопротивления (<see cref="ResistanceRemoved"/>).
    /// Внутренний метод, вызывается при восстановлении проекции или в ответ на событие.
    /// </summary>
    internal void Apply(ResistanceRemoved e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var res = dto.Resistances.Where(r => r != e.DamageType).ToList();
            _state[e.CharacterId] = dto with { Resistances = res };
        }
    }
}