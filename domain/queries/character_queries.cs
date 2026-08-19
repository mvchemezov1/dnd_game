// domain/queries/character_queries.cs
using dnd_game.Application.Projections; // DTO лежат здесь

namespace dnd_game.Domain.Queries;

public record GetCharacterById(Guid CharacterId) : IQuery<CharacterDto?>;
public record GetAllCharacters : IQuery<List<CharacterDto>>;
public record GetCharacterHitPoints(Guid CharacterId) : IQuery<CharacterHitPointsDto?>;
public record GetCharacterCombatStats(Guid CharacterId) : IQuery<CharacterCombatStatsDto?>;
public record GetCharacterSpells(Guid CharacterId) : IQuery<CharacterSpellsDto?>;
public record GetCharacterInventory(Guid CharacterId) : IQuery<List<InventoryItemDto>>;
public record GetCharacterEquipment(Guid CharacterId) : IQuery<List<EquippedItemDto>>;
public record GetCharacterDeathStatus(Guid CharacterId) : IQuery<CharacterDeathStatusDto?>;
public record GetCharacterConditions(Guid CharacterId) : IQuery<List<string>>;
public record GetCharacterDefenses(Guid CharacterId) : IQuery<CharacterDefensesDto?>;
public record SearchCharacters(
    string? NameFilter = null,
    string? ClassFilter = null,
    string? RaceFilter = null,
    bool? IsAliveFilter = null,
    int? MinLevel = null,
    int? MaxLevel = null
) : IQuery<List<CharacterSummaryDto>>;