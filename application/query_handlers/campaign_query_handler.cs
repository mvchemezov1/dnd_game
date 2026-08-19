// application/query_handlers/campaign_query_handler.cs
using dnd_game.Domain.Queries;
using dnd_game.Application.Projections;

namespace dnd_game.application.query_handlers;

/// <summary>
/// Обработчик запросов, связанных с кампанией. Делегирует выполнение запросов
/// проекции <see cref="CampaignProjection"/>, которая содержит уже построенную read-модель.
/// Реализует паттерн CQRS: команды изменяют состояние, а запросы только читают данные.
/// </summary>
/// <remarks>
/// Каждый метод просто вызывает соответствующий метод проекции и возвращает результат.
/// Никакой бизнес-логики или проверок здесь не выполняется — вся логика уже заложена в проекции.
/// </remarks>
/// <param name="projection">Проекция кампании, содержащая актуальное состояние read-модели.</param>
public class CampaignQueryHandler(CampaignProjection projection) : IQueryHandler<GetActiveQuests, List<Guid>>,
                                    IQueryHandler<GetQuestDetails, QuestInfo?>,
                                    IQueryHandler<GetQuestsByStatus, List<QuestInfo>>,
                                    IQueryHandler<GetCampaignState, CampaignState?>,
                                    IQueryHandler<GetFactionReputation, FactionState?>,
                                    IQueryHandler<GetAllFactions, List<FactionState>>,
                                    IQueryHandler<GetActiveWorldEvents, List<string>>
{
    /// <summary>
    /// Обрабатывает запрос <see cref="GetActiveQuests"/>: получает список идентификаторов активных квестов кампании.
    /// </summary>
    /// <param name="query">Запрос, содержащий идентификатор кампании.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <returns>Список идентификаторов активных квестов.</returns>
    public Task<List<Guid>> Handle(GetActiveQuests query, CancellationToken cancellationToken)
        => projection.GetActiveQuestIds(query.CampaignId);

    /// <summary>
    /// Обрабатывает запрос <see cref="GetQuestDetails"/>: получает детальную информацию о конкретном квесте.
    /// </summary>
    /// <param name="query">Запрос, содержащий идентификатор кампании и идентификатор квеста.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <returns>Объект <see cref="QuestInfo"/> или null, если квест не найден.</returns>
    public Task<QuestInfo?> Handle(GetQuestDetails query, CancellationToken cancellationToken)
        => projection.GetQuestDetails(query.CampaignId, query.QuestId);

    /// <summary>
    /// Обрабатывает запрос <see cref="GetQuestsByStatus"/>: получает список квестов, отфильтрованных по статусу.
    /// </summary>
    /// <param name="query">Запрос, содержащий идентификатор кампании и фильтр по статусу.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <returns>Список квестов, соответствующих указанному статусу.</returns>
    public Task<List<QuestInfo>> Handle(GetQuestsByStatus query, CancellationToken cancellationToken)
        => projection.GetQuests(query.CampaignId, query.StatusFilter);

    /// <summary>
    /// Обрабатывает запрос <see cref="GetCampaignState"/>: получает текущее состояние кампании.
    /// Включает такие данные, как игровой день, час, погода, открытые регионы и глобальные флаги.
    /// </summary>
    /// <param name="query">Запрос, содержащий идентификатор кампании.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <returns>Объект <see cref="CampaignState"/> или null, если кампания не найдена.</returns>
    public Task<CampaignState?> Handle(GetCampaignState query, CancellationToken cancellationToken)
        => projection.GetCampaignState(query.CampaignId);

    /// <summary>
    /// Обрабатывает запрос <see cref="GetFactionReputation"/>: получает состояние конкретной фракции.
    /// </summary>
    /// <param name="query">Запрос, содержащий идентификатор фракции.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <returns>Объект <see cref="FactionState"/> или null, если фракция не найдена.</returns>
    public Task<FactionState?> Handle(GetFactionReputation query, CancellationToken cancellationToken)
        => projection.GetFaction(query.FactionId);

    /// <summary>
    /// Обрабатывает запрос <see cref="GetAllFactions"/>: получает список всех известных фракций с их репутацией.
    /// </summary>
    /// <param name="query">Запрос (без параметров).</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <returns>Список состояний всех фракций.</returns>
    public Task<List<FactionState>> Handle(GetAllFactions query, CancellationToken cancellationToken)
        => projection.GetAllFactions();

    /// <summary>
    /// Обрабатывает запрос <see cref="GetActiveWorldEvents"/>: получает список активных мировых событий кампании.
    /// </summary>
    /// <param name="query">Запрос, содержащий идентификатор кампании.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <returns>Список названий активных мировых событий.</returns>
    public Task<List<string>> Handle(GetActiveWorldEvents query, CancellationToken cancellationToken)
        => projection.GetActiveWorldEvents(query.CampaignId);
}