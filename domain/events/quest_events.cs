// domain/events/quest_events.cs
namespace dnd_game.Domain.Events;

// ---------- Кампания (создание) ----------
public record CampaignCreated(Guid CampaignId, string Name, Guid GameMasterId, DateTime Timestamp) : IDomainEvent;

// ---------- Управление игроками в кампании ----------
public record PlayerJoinedCampaign(Guid CampaignId, Guid PlayerId, DateTime Timestamp) : IDomainEvent;
public record PlayerLeftCampaign(Guid CampaignId, Guid PlayerId, DateTime Timestamp) : IDomainEvent;

// ---------- Квесты: жизненный цикл ----------
public record QuestCreated(Guid CampaignId, Guid QuestId, string Title, string Description,
    List<QuestObjectiveData> Objectives, List<QuestRewardData> Rewards, List<Guid> ParticipantIds, DateTime IssuedAt) : IDomainEvent;
public record QuestAccepted(Guid CampaignId, Guid QuestId, List<Guid> ParticipantIds, DateTime Timestamp) : IDomainEvent;
public record QuestCompleted(Guid CampaignId, Guid QuestId, DateTime Timestamp) : IDomainEvent;
public record QuestFailed(Guid CampaignId, Guid QuestId, DateTime Timestamp) : IDomainEvent;
public record QuestAbandoned(Guid CampaignId, Guid QuestId, DateTime Timestamp) : IDomainEvent;

// ---------- Цели квеста ----------
public record QuestObjectiveUpdated(Guid CampaignId, Guid QuestId, int ObjectiveIndex, bool IsCompleted, int CurrentProgress) : IDomainEvent;

// ---------- Награды квеста ----------
public record QuestRewardClaimed(Guid CampaignId, Guid QuestId, Guid CharacterId, int ExperiencePoints, int Gold, List<string> ItemIds, string? FactionReputationChange) : IDomainEvent;

// ---------- Фракции ----------
public record FactionAdded(Guid CampaignId, string FactionId, int InitialReputation) : IDomainEvent;
public record FactionReputationChanged(Guid CampaignId, string FactionId, int Change) : IDomainEvent;

// ---------- Глобальные флаги ----------
public record GlobalFlagSet(Guid CampaignId, string FlagName, string FlagValue) : IDomainEvent;
public record GlobalFlagRemoved(Guid CampaignId, string FlagName) : IDomainEvent;

// ---------- Игровое время и погода ----------
public record GameTimeAdvanced(Guid CampaignId, int Minutes) : IDomainEvent;
public record WeatherChanged(Guid CampaignId, string NewWeather) : IDomainEvent;

// ---------- Исследование регионов ----------
public record RegionDiscovered(Guid CampaignId, string RegionName) : IDomainEvent;

// ---------- Вспомогательные типы данных (можно оставить здесь или в отдельном файле) ----------
public record FactionDiscovered(Guid CampaignId, string FactionId, string FactionName, DateTime OccurredOn) : IDomainEvent;
public record WorldEventTriggered(Guid CampaignId, string EventName, DateTime OccurredOn) : IDomainEvent;
public class QuestObjectiveData
{
    public string Description { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int CurrentProgress { get; set; }
    public int RequiredProgress { get; set; }
}

public class QuestRewardData
{
    public string Description { get; set; } = string.Empty;
    public int ExperiencePoints { get; set; }
    public List<string> ItemIds { get; set; } = [];
    public int Gold { get; set; }
    public string? FactionReputationChange { get; set; }
}

public record ItemAcquired(Guid CharacterId, string ItemId, int Quantity, DateTime OccurredOn) : IDomainEvent;