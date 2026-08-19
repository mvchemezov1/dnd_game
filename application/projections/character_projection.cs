// application/projections/character_projection.cs
using dnd_game.Domain.Events;
using dnd_game.Domain.Queries;
using dnd_game.Infrastructure.Caching;
using dnd_game.Infrastructure.EventStore;
using System.Collections.Concurrent;

namespace dnd_game.Application.Projections;

public class CharacterProjection
{
    private readonly ConcurrentDictionary<Guid, CharacterDto> _state = new();
    private readonly ICacheProvider _cache;
    private readonly TimeSpan _cacheTtl;

    public CharacterProjection(ICacheProvider cache, TimeSpan? cacheTtl = null)
    {
        _cache = cache;
        _cacheTtl = cacheTtl ?? TimeSpan.FromMinutes(5);
    }

    private void InvalidateCache(Guid characterId)
    {
        _ = Task.Run(async () =>
        {
            await _cache.RemoveAsync($"character:{characterId}");
            await _cache.RemoveAsync("characters:all");
        });
    }

    // ---------- Базовые события создания и обновления ----------
    public void Apply(CharacterCreated e)
    {
        _state[e.CharacterId] = new CharacterDto(
            Id: e.CharacterId,
            Name: e.Name,
            MaxHitPoints: e.MaxHitPoints,
            HitPoints: e.MaxHitPoints,
            AbilityScores: new Dictionary<string, int>(),
            SkillProficiencies: new Dictionary<string, bool>(),
            SavingThrowProficiencies: new Dictionary<string, bool>(),
            KnownSpells: new List<string>(),
            SpellSlots: new Dictionary<int, int>(),
            UsedSpellSlots: new Dictionary<int, int>(),
            HitDice: new Dictionary<int, int>(),
            Conditions: new List<string>(),
            Resistances: new List<string>(),
            Vulnerabilities: new List<string>(),
            Immunities: new List<string>(),
            Equipment: new List<EquippedItemDto>(),
            Inventory: new List<InventoryItemDto>(),
            Feats: new List<string>()
        );
        InvalidateCache(e.CharacterId);
    }

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
    public void Apply(ExperienceGained e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { ExperiencePoints = dto.ExperiencePoints + e.Amount };
            InvalidateCache(e.CharacterId);
        }
    }

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
    public void Apply(SkillProficiencyAdded e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var skills = new Dictionary<string, bool>((IDictionary<string, bool>)dto.SkillProficiencies) { [e.Skill] = true };
            _state[e.CharacterId] = dto with { SkillProficiencies = skills };
            InvalidateCache(e.CharacterId);
        }
    }

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

    public void Apply(SavingThrowProficiencyAdded e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var saves = new Dictionary<string, bool>((IDictionary<string, bool>)dto.SavingThrowProficiencies) { [e.Ability] = true };
            _state[e.CharacterId] = dto with { SavingThrowProficiencies = saves };
            InvalidateCache(e.CharacterId);
        }
    }

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
    public void Apply(RaceChosen e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { Race = e.Race };
            InvalidateCache(e.CharacterId);
        }
    }

    public void Apply(ClassChosen e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { Class = e.ClassName };
            InvalidateCache(e.CharacterId);
        }
    }

    public void Apply(BackgroundChosen e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { Background = e.BackgroundName };
            InvalidateCache(e.CharacterId);
        }
    }

    // ---------- Черты ----------
    public void Apply(FeatAdded e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var feats = new List<string>(dto.Feats) { e.FeatName };
            _state[e.CharacterId] = dto with { Feats = feats };
            InvalidateCache(e.CharacterId);
        }
    }

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
    public void Apply(SpellAdded e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var spells = new List<string>(dto.KnownSpells) { e.SpellId };
            _state[e.CharacterId] = dto with { KnownSpells = spells };
            InvalidateCache(e.CharacterId);
        }
    }

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

    public void Apply(SpellSlotsRestored e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { UsedSpellSlots = new Dictionary<int, int>() };
            InvalidateCache(e.CharacterId);
        }
    }

    // ---------- Состояния ----------
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

    public void Apply(AllConditionsCleared e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { Conditions = new List<string>() };
            InvalidateCache(e.CharacterId);
        }
    }

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
    public void Apply(ArmorClassUpdated e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { ArmorClass = e.NewArmorClass };
            InvalidateCache(e.CharacterId);
        }
    }

    public void Apply(SpeedUpdated e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { Speed = e.NewSpeed };
            InvalidateCache(e.CharacterId);
        }
    }

    // ---------- Сопротивления, уязвимости, иммунитеты ----------
    public void Apply(ResistanceAdded e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var res = new List<string>(dto.Resistances) { e.DamageType };
            _state[e.CharacterId] = dto with { Resistances = res };
            InvalidateCache(e.CharacterId);
        }
    }

    public void Apply(VulnerabilityAdded e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var vul = new List<string>(dto.Vulnerabilities) { e.DamageType };
            _state[e.CharacterId] = dto with { Vulnerabilities = vul };
            InvalidateCache(e.CharacterId);
        }
    }

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

    public void Apply(CharacterDied e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { IsDead = true };
            InvalidateCache(e.CharacterId);
        }
    }

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
    public void Apply(GoldAdded e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { Gold = dto.Gold + e.Amount };
            InvalidateCache(e.CharacterId);
        }
    }

    public void Apply(GoldSpent e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { Gold = Math.Max(0, dto.Gold - e.Amount) };
            InvalidateCache(e.CharacterId);
        }
    }

    public void Apply(GoldSet e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { Gold = e.Amount };
            InvalidateCache(e.CharacterId);
        }
    }

    // ---------- Кости хитов и отдых ----------
    public void Apply(HitDieSpent e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var dice = new Dictionary<int, int>(dto.HitDice);
            if (dice.ContainsKey(e.HitDieType))
                dice[e.HitDieType] = Math.Max(0, dice[e.HitDieType] - 1);
            _state[e.CharacterId] = dto with { HitDice = dice };
            InvalidateCache(e.CharacterId);
        }
    }

    public void Apply(HitDiceRecovered e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var dice = new Dictionary<int, int>(dto.HitDice);
            foreach (var kv in e.Recovered)
            {
                if (dice.ContainsKey(kv.Key))
                    dice[kv.Key] = Math.Min(dto.MaxHitDice.GetValueOrDefault(kv.Key), dice[kv.Key] + kv.Value);
                else
                    dice[kv.Key] = kv.Value;
            }
            _state[e.CharacterId] = dto with { HitDice = dice };
            InvalidateCache(e.CharacterId);
        }
    }

    // ---------- Концентрация ----------
    public void Apply(ConcentrationStarted e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { Concentrating = true };
            InvalidateCache(e.CharacterId);
        }
    }

    public void Apply(ConcentrationEnded e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            _state[e.CharacterId] = dto with { Concentrating = false };
            InvalidateCache(e.CharacterId);
        }
    }

    // ---------- Методы доступа (с кешем) ----------
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

    internal void Apply(VulnerabilityRemoved e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var vul = dto.Vulnerabilities.Where(v => v != e.DamageType).ToList();
            _state[e.CharacterId] = dto with { Vulnerabilities = vul };
        }
    }

    internal void Apply(ImmunityRemoved e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var imm = dto.Immunities.Where(i => i != e.DamageType).ToList();
            _state[e.CharacterId] = dto with { Immunities = imm };
        }
    }

    internal void Apply(ResistanceRemoved e)
    {
        if (_state.TryGetValue(e.CharacterId, out var dto))
        {
            var res = dto.Resistances.Where(r => r != e.DamageType).ToList();
            _state[e.CharacterId] = dto with { Resistances = res };
        }
    }
}