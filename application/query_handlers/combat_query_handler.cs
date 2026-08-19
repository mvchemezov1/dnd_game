// application/query_handlers/combat_query_handler.cs
using dnd_game.Domain.Queries;
using dnd_game.Application.Projections;

namespace dnd_game.application.query_handlers;

/// <summary>
/// Обработчик запросов, связанных с боевыми сценами. Делегирует выполнение запросов
/// проекции <see cref="CombatProjection"/>, которая содержит актуальную read-модель.
/// Реализует паттерн CQRS: команды изменяют состояние, а запросы только читают данные.
/// </summary>
/// <remarks>
/// Все методы обращаются к проекции и возвращают готовые DTO или производные значения.
/// Бизнес-логика отсутствует.
/// </remarks>
/// <param name="projection">Проекция боевых сцен, содержащая актуальное состояние read-модели.</param>
public class CombatQueryHandler(CombatProjection projection) : IQueryHandler<GetCombatStatus, CombatStatusDto?>,
                                 IQueryHandler<GetCombatParticipants, List<CombatParticipantDto>>,
                                 IQueryHandler<GetCurrentCombatParticipant, CombatParticipantDto?>,
                                 IQueryHandler<GetCombatRound, int>,
                                 IQueryHandler<GetCombatTurnOrder, List<Guid>>,
                                 IQueryHandler<IsCombatActive, bool>
{
    /// <summary>
    /// Обрабатывает запрос <see cref="GetCombatStatus"/>: получает полный статус боевой сцены.
    /// </summary>
    /// <param name="query">Запрос, содержащий идентификатор боя.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <returns>Объект <see cref="CombatStatusDto"/> или null, если бой не найден.</returns>
    public Task<CombatStatusDto?> Handle(GetCombatStatus query, CancellationToken cancellationToken)
        => projection.GetStatus(query.CombatId);

    /// <summary>
    /// Обрабатывает запрос <see cref="GetCombatParticipants"/>: получает список всех участников боя.
    /// </summary>
    /// <param name="query">Запрос, содержащий идентификатор боя.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <returns>Список DTO участников боя.</returns>
    public async Task<List<CombatParticipantDto>> Handle(GetCombatParticipants query, CancellationToken cancellationToken)
        => await projection.GetParticipants(query.CombatId);

    /// <summary>
    /// Обрабатывает запрос <see cref="GetCurrentCombatParticipant"/>: получает текущего активного участника (чей сейчас ход).
    /// </summary>
    /// <param name="query">Запрос, содержащий идентификатор боя.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <returns>DTO текущего участника или null, если ход ничей или бой не найден.</returns>
    public async Task<CombatParticipantDto?> Handle(GetCurrentCombatParticipant query, CancellationToken cancellationToken)
        => await projection.GetCurrentParticipant(query.CombatId);

    /// <summary>
    /// Обрабатывает запрос <see cref="GetCombatRound"/>: получает номер текущего раунда.
    /// </summary>
    /// <param name="query">Запрос, содержащий идентификатор боя.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <returns>Номер текущего раунда (0, если бой не найден).</returns>
    public async Task<int> Handle(GetCombatRound query, CancellationToken cancellationToken)
    {
        var status = await projection.GetStatus(query.CombatId);
        return status?.Round ?? 0;
    }

    /// <summary>
    /// Обрабатывает запрос <see cref="GetCombatTurnOrder"/>: получает порядок ходов
    /// в виде списка идентификаторов персонажей, отсортированных по инициативе.
    /// </summary>
    /// <param name="query">Запрос, содержащий идентификатор боя.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <returns>Список идентификаторов персонажей в порядке хода.</returns>
    public async Task<List<Guid>> Handle(GetCombatTurnOrder query, CancellationToken cancellationToken)
    {
        var participants = await projection.GetParticipants(query.CombatId);
        return [.. participants.Select(p => p.CharacterId)];
    }

    /// <summary>
    /// Обрабатывает запрос <see cref="IsCombatActive"/>: проверяет, активен ли бой.
    /// </summary>
    /// <param name="query">Запрос, содержащий идентификатор боя.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <returns>True, если бой активен; иначе False (в том числе если бой не найден).</returns>
    public async Task<bool> Handle(IsCombatActive query, CancellationToken cancellationToken)
    {
        var status = await projection.GetStatus(query.CombatId);
        return status?.IsActive ?? false;
    }
}