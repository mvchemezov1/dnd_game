// application/query_handlers/character_query_handler.cs
using dnd_game.Domain.Queries;
using dnd_game.Application.Projections;

namespace dnd_game.application.query_handlers;

/// <summary>
/// Обработчик запросов, связанных с персонажами. Делегирует выполнение запросов
/// проекции <see cref="CharacterProjection"/>, которая содержит актуальную read-модель.
/// Реализует паттерн CQRS: команды изменяют состояние, а запросы только читают данные.
/// </summary>
/// <remarks>
/// Все методы обращаются к проекции и при необходимости преобразуют полученный DTO
/// в специализированные DTO для конкретных запросов. Бизнес-логика отсутствует.
/// </remarks>
/// <param name="projection">Проекция персонажей, содержащая актуальное состояние read-модели.</param>
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
    /// <summary>
    /// Обрабатывает запрос <see cref="GetCharacterById"/>: получает полную информацию о персонаже по идентификатору.
    /// </summary>
    /// <param name="q">Запрос, содержащий идентификатор персонажа.</param>
    /// <param name="ct">Токен для уведомления об отмене операции.</param>
    /// <returns>Объект <see cref="CharacterDto"/> или null, если персонаж не найден.</returns>
    public Task<CharacterDto?> Handle(GetCharacterById q, CancellationToken ct)
        => projection.GetById(q.CharacterId);

    /// <summary>
    /// Обрабатывает запрос <see cref="GetAllCharacters"/>: получает список всех персонажей.
    /// </summary>
    /// <param name="q">Запрос без параметров.</param>
    /// <param name="ct">Токен для уведомления об отмене операции.</param>
    /// <returns>Список DTO всех персонажей.</returns>
    public Task<List<CharacterDto>> Handle(GetAllCharacters q, CancellationToken ct)
        => projection.GetAll();

    /// <summary>
    /// Обрабатывает запрос <see cref="GetCharacterHitPoints"/>: получает информацию о хитах персонажа.
    /// </summary>
    /// <param name="q">Запрос, содержащий идентификатор персонажа.</param>
    /// <param name="ct">Токен для уведомления об отмене операции.</param>
    /// <returns>Объект <see cref="CharacterHitPointsDto"/> или null, если персонаж не найден.</returns>
    public async Task<CharacterHitPointsDto?> Handle(GetCharacterHitPoints q, CancellationToken ct)
    {
        var c = await projection.GetById(q.CharacterId);
        return c is null ? null : new CharacterHitPointsDto(c.HitPoints, c.MaxHitPoints, c.TemporaryHitPoints);
    }

    /// <summary>
    /// Обрабатывает запрос <see cref="GetCharacterCombatStats"/>: получает боевые характеристики персонажа.
    /// </summary>
    /// <param name="q">Запрос, содержащий идентификатор персонажа.</param>
    /// <param name="ct">Токен для уведомления об отмене операции.</param>
    /// <returns>Объект <see cref="CharacterCombatStatsDto"/> или null, если персонаж не найден.</returns>
    public async Task<CharacterCombatStatsDto?> Handle(GetCharacterCombatStats q, CancellationToken ct)
    {
        var c = await projection.GetById(q.CharacterId);
        return c is null ? null : new CharacterCombatStatsDto(c.ArmorClass, c.Speed, c.HitDiceRemaining, c.DeathSaveSuccesses, c.DeathSaveFailures, c.IsStable);
    }

    /// <summary>
    /// Обрабатывает запрос <see cref="GetCharacterSpells"/>: получает информацию о заклинаниях персонажа.
    /// </summary>
    /// <param name="q">Запрос, содержащий идентификатор персонажа.</param>
    /// <param name="ct">Токен для уведомления об отмене операции.</param>
    /// <returns>Объект <see cref="CharacterSpellsDto"/> или null, если персонаж не найден.</returns>
    public async Task<CharacterSpellsDto?> Handle(GetCharacterSpells q, CancellationToken ct)
    {
        var c = await projection.GetById(q.CharacterId);
        return c is null ? null : new CharacterSpellsDto(c.KnownSpells, c.MaxSpellSlots, c.UsedSpellSlots);
    }

    /// <summary>
    /// Обрабатывает запрос <see cref="GetCharacterInventory"/>: получает список предметов в инвентаре.
    /// </summary>
    /// <param name="q">Запрос, содержащий идентификатор персонажа.</param>
    /// <param name="ct">Токен для уведомления об отмене операции.</param>
    /// <returns>Список предметов инвентаря; пустой список, если персонаж не найден.</returns>
    public async Task<List<InventoryItemDto>> Handle(GetCharacterInventory q, CancellationToken ct)
    {
        var c = await projection.GetById(q.CharacterId);
        return c?.Inventory ?? [];
    }

    /// <summary>
    /// Обрабатывает запрос <see cref="GetCharacterEquipment"/>: получает список экипированных предметов.
    /// </summary>
    /// <param name="q">Запрос, содержащий идентификатор персонажа.</param>
    /// <param name="ct">Токен для уведомления об отмене операции.</param>
    /// <returns>Список экипированных предметов; пустой список, если персонаж не найден.</returns>
    public async Task<List<EquippedItemDto>> Handle(GetCharacterEquipment q, CancellationToken ct)
    {
        var c = await projection.GetById(q.CharacterId);
        return c?.Equipment ?? [];
    }

    /// <summary>
    /// Обрабатывает запрос <see cref="GetCharacterDeathStatus"/>: определяет статус жизни/смерти персонажа.
    /// </summary>
    /// <param name="q">Запрос, содержащий идентификатор персонажа.</param>
    /// <param name="ct">Токен для уведомления об отмене операции.</param>
    /// <returns>Объект <see cref="CharacterDeathStatusDto"/> или null, если персонаж не найден.</returns>
    public async Task<CharacterDeathStatusDto?> Handle(GetCharacterDeathStatus q, CancellationToken ct)
    {
        var c = await projection.GetById(q.CharacterId);
        if (c is null) return null;
        string status = c.IsDead ? "Dead" : c.HitPoints > 0 ? "Alive" : c.IsStable ? "Stable" : "Dying";
        return new CharacterDeathStatusDto(status, c.DeathSaveSuccesses, c.DeathSaveFailures);
    }

    /// <summary>
    /// Обрабатывает запрос <see cref="GetCharacterConditions"/>: получает список активных состояний персонажа.
    /// </summary>
    /// <param name="q">Запрос, содержащий идентификатор персонажа.</param>
    /// <param name="ct">Токен для уведомления об отмене операции.</param>
    /// <returns>Список строк состояний; пустой список, если персонаж не найден.</returns>
    public async Task<List<string>> Handle(GetCharacterConditions q, CancellationToken ct)
    {
        var c = await projection.GetById(q.CharacterId);
        return c?.Conditions ?? [];
    }

    /// <summary>
    /// Обрабатывает запрос <see cref="GetCharacterDefenses"/>: получает информацию о защитах персонажа.
    /// </summary>
    /// <param name="q">Запрос, содержащий идентификатор персонажа.</param>
    /// <param name="ct">Токен для уведомления об отмене операции.</param>
    /// <returns>Объект <see cref="CharacterDefensesDto"/> или null, если персонаж не найден.</returns>
    public async Task<CharacterDefensesDto?> Handle(GetCharacterDefenses q, CancellationToken ct)
    {
        var c = await projection.GetById(q.CharacterId);
        return c is null ? null : new CharacterDefensesDto(c.Resistances, c.Vulnerabilities, c.Immunities);
    }

    /// <summary>
    /// Обрабатывает запрос <see cref="SearchCharacters"/>: выполняет поиск персонажей по заданным фильтрам.
    /// </summary>
    /// <param name="q">Запрос, содержащий критерии фильтрации: имя, класс, раса, уровень, признак жизни.</param>
    /// <param name="ct">Токен для уведомления об отмене операции.</param>
    /// <returns>Список кратких сводок (<see cref="CharacterSummaryDto"/>) персонажей, удовлетворяющих фильтрам.</returns>
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

        return [.. filtered.Select(c => new CharacterSummaryDto(
            c.Id, c.Name, c.Level, c.Class, c.Race,
            c.HitPoints, c.MaxHitPoints, !c.IsDead, c.ArmorClass
        ))];
    }
}