// domain/queries/campaign_queries.cs
using dnd_game.Application.Projections; // для типов, таких как QuestInfo, CampaignState, FactionState

namespace dnd_game.Domain.Queries
{
    // --------------------------------------------------------------------------------------------
    // Активные квесты (уже было)
    // --------------------------------------------------------------------------------------------
    public record GetActiveQuests(Guid CampaignId) : IQuery<List<Guid>>;

    // --------------------------------------------------------------------------------------------
    // Детали конкретного квеста
    // --------------------------------------------------------------------------------------------
    public record GetQuestDetails(Guid CampaignId, Guid QuestId) : IQuery<QuestInfo?>;

    // --------------------------------------------------------------------------------------------
    // Список квестов с фильтрацией по статусу
    // --------------------------------------------------------------------------------------------
    public record GetQuestsByStatus(Guid CampaignId, QuestStatus? StatusFilter = null) : IQuery<List<QuestInfo>>;

    // --------------------------------------------------------------------------------------------
    // Состояние кампании (день, погода, флаги, открытые регионы)
    // --------------------------------------------------------------------------------------------
    public record GetCampaignState(Guid CampaignId) : IQuery<CampaignState?>;

    // --------------------------------------------------------------------------------------------
    // Репутация фракций
    // --------------------------------------------------------------------------------------------
    public record GetFactionReputation(string FactionId) : IQuery<FactionState?>;

    public record GetAllFactions : IQuery<List<FactionState>>;

    // --------------------------------------------------------------------------------------------
    // Мировые события
    // --------------------------------------------------------------------------------------------
    public record GetActiveWorldEvents(Guid CampaignId) : IQuery<List<string>>;

    // --------------------------------------------------------------------------------------------
    // Игровое время
    // --------------------------------------------------------------------------------------------
    public record GetCurrentGameTime(Guid CampaignId) : IQuery<GameTimeDto>;

    public record GameTimeDto(int Day, int Hour, int Minute);

    // --------------------------------------------------------------------------------------------
    // Регионы
    // --------------------------------------------------------------------------------------------
    public record GetDiscoveredRegions(Guid CampaignId) : IQuery<List<string>>;

    // --------------------------------------------------------------------------------------------
    // Глобальные флаги
    // --------------------------------------------------------------------------------------------
    public record GetGlobalFlag(Guid CampaignId, string FlagName) : IQuery<string?>;
    public record GetAllGlobalFlags(Guid CampaignId) : IQuery<Dictionary<string, string>>;

    // --------------------------------------------------------------------------------------------
    // Поиск квестов (с пагинацией)
    // --------------------------------------------------------------------------------------------
    public record SearchQuests(
        Guid CampaignId,
        string? TitleFilter = null,
        QuestStatus? StatusFilter = null,
        int PageNumber = 1,
        int PageSize = 20
    ) : IQuery<PagedResult<QuestInfo>>;
}