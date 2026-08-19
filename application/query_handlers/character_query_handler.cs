// application/query_handlers/character_query_handler.cs
using dnd_game.Domain.Queries;
using dnd_game.Application.Projections;

namespace dnd_game.application.query_handlers;

public class CharacterQueryHandler(CharacterProjection projection) : IQueryHandler<GetCharacterById, CharacterDto?>,
                                     IQueryHandler<GetAllCharacters, List<CharacterDto>>,
                                     IQueryHandler<GetCharacterHitPoints, CharacterHitPointsDto?>,
                                     IQueryHandler<GetCharacterCombatStats, CharacterCombatStatsDto?>,
                                     IQueryHandler<GetCharacterSpells, CharacterSpellsDto?>,
                                     IQueryHandler<GetCharacterInventory, List<InventoryItemDto>>,
                                     IQueryHandler<GetCharacterEquipment, List<EquippedItemDto>>,
                                     IQueryHandler<GetCharacterDeathStatus, CharacterDeathStatusDto?>,
                                     IQueryHandler<GetCharacterConditions, List<string>>,
                                     IQueryHandler<GetCharacterDefenses, CharacterDefensesDto?>,
                                     IQueryHandler<SearchCharacters, List<CharacterSummaryDto>>
{
    public Task<CharacterDto?> Handle(GetCharacterById q, CancellationToken ct)
        => projection.GetById(q.CharacterId);

    public Task<List<CharacterDto>> Handle(GetAllCharacters q, CancellationToken ct)
        => projection.GetAll();

    public async Task<CharacterHitPointsDto?> Handle(GetCharacterHitPoints q, CancellationToken ct)
    {
        var c = await projection.GetById(q.CharacterId);
        return c is null ? null : new CharacterHitPointsDto(c.HitPoints, c.MaxHitPoints, c.TemporaryHitPoints);
    }

    public async Task<CharacterCombatStatsDto?> Handle(GetCharacterCombatStats q, CancellationToken ct)
    {
        var c = await projection.GetById(q.CharacterId);
        return c is null ? null : new CharacterCombatStatsDto(c.ArmorClass, c.Speed, c.HitDiceRemaining, c.DeathSaveSuccesses, c.DeathSaveFailures, c.IsStable);
    }

    public async Task<CharacterSpellsDto?> Handle(GetCharacterSpells q, CancellationToken ct)
    {
        var c = await projection.GetById(q.CharacterId);
        return c is null ? null : new CharacterSpellsDto(c.KnownSpells, c.MaxSpellSlots, c.UsedSpellSlots);
    }

    public async Task<List<InventoryItemDto>> Handle(GetCharacterInventory q, CancellationToken ct)
    {
        var c = await projection.GetById(q.CharacterId);
        return c?.Inventory ?? [];
    }

    public async Task<List<EquippedItemDto>> Handle(GetCharacterEquipment q, CancellationToken ct)
    {
        var c = await projection.GetById(q.CharacterId);
        return c?.Equipment ?? [];
    }

    public async Task<CharacterDeathStatusDto?> Handle(GetCharacterDeathStatus q, CancellationToken ct)
    {
        var c = await projection.GetById(q.CharacterId);
        if (c is null) return null;
        string status = c.IsDead ? "Dead" : c.HitPoints > 0 ? "Alive" : c.IsStable ? "Stable" : "Dying";
        return new CharacterDeathStatusDto(status, c.DeathSaveSuccesses, c.DeathSaveFailures);
    }

    public async Task<List<string>> Handle(GetCharacterConditions q, CancellationToken ct)
    {
        var c = await projection.GetById(q.CharacterId);
        return c?.Conditions ?? [];
    }

    public async Task<CharacterDefensesDto?> Handle(GetCharacterDefenses q, CancellationToken ct)
    {
        var c = await projection.GetById(q.CharacterId);
        return c is null ? null : new CharacterDefensesDto(c.Resistances, c.Vulnerabilities, c.Immunities);
    }

    public async Task<List<CharacterSummaryDto>> Handle(SearchCharacters q, CancellationToken ct)
    {
        var all = await projection.GetAll();
        var filtered = all.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(q.NameFilter))
            filtered = filtered.Where(c => c.Name.Contains(q.NameFilter, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(q.ClassFilter))
            filtered = filtered.Where(c => c.Class.Equals(q.ClassFilter, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(q.RaceFilter))
            filtered = filtered.Where(c => c.Race.Equals(q.RaceFilter, StringComparison.OrdinalIgnoreCase));
        if (q.IsAliveFilter.HasValue)
            filtered = filtered.Where(c => c.IsDead != q.IsAliveFilter.Value);
        if (q.MinLevel.HasValue)
            filtered = filtered.Where(c => c.Level >= q.MinLevel.Value);
        if (q.MaxLevel.HasValue)
            filtered = filtered.Where(c => c.Level <= q.MaxLevel.Value);
        return filtered.Select(c => new CharacterSummaryDto(c.Id, c.Name, c.Level, c.Class, c.Race, c.HitPoints, c.MaxHitPoints, !c.IsDead, c.ArmorClass)).ToList();
    }
}