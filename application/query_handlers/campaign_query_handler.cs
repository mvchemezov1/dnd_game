// application/query_handlers/campaign_query_handler.cs
using dnd_game.Domain.Queries;
using dnd_game.Application.Projections;

namespace dnd_game.application.query_handlers;

public class CampaignQueryHandler(CampaignProjection projection) : IQueryHandler<GetActiveQuests, List<Guid>>,
                                    IQueryHandler<GetQuestDetails, QuestInfo?>,
                                    IQueryHandler<GetQuestsByStatus, List<QuestInfo>>,
                                    IQueryHandler<GetCampaignState, CampaignState?>,
                                    IQueryHandler<GetFactionReputation, FactionState?>,
                                    IQueryHandler<GetAllFactions, List<FactionState>>,
                                    IQueryHandler<GetActiveWorldEvents, List<string>>
{

    // Получить список идентификаторов активных квестов
    public Task<List<Guid>> Handle(GetActiveQuests query, CancellationToken cancellationToken)
        => projection.GetActiveQuestIds(query.CampaignId);

    // Получить детальную информацию о конкретном квесте
    public Task<QuestInfo?> Handle(GetQuestDetails query, CancellationToken cancellationToken)
        => projection.GetQuestDetails(query.CampaignId, query.QuestId);

    // Получить список квестов, отфильтрованных по статусу
    public Task<List<QuestInfo>> Handle(GetQuestsByStatus query, CancellationToken cancellationToken)
        => projection.GetQuests(query.CampaignId, query.StatusFilter);

    // Получить текущее состояние кампании (день, погода, флаги и т.д.)
    public Task<CampaignState?> Handle(GetCampaignState query, CancellationToken cancellationToken)
        => projection.GetCampaignState(query.CampaignId);

    // Получить репутацию конкретной фракции
    public Task<FactionState?> Handle(GetFactionReputation query, CancellationToken cancellationToken)
        => projection.GetFaction(query.FactionId);

    // Получить список всех известных фракций с их репутацией
    public Task<List<FactionState>> Handle(GetAllFactions query, CancellationToken cancellationToken)
        => projection.GetAllFactions();

    // Получить список активных мировых событий
    public Task<List<string>> Handle(GetActiveWorldEvents query, CancellationToken cancellationToken)
        => projection.GetActiveWorldEvents(query.CampaignId);
}