// domain/commands/campaign_commands.cs
using dnd_game.Domain.Events;

namespace dnd_game.Domain.Commands;

// ---------- Управление квестами ----------

/// <summary>
/// Создать новый квест в кампании.
/// </summary>
public record CreateQuestCommand(
    Guid CampaignId,
    Guid QuestId,
    string Title,
    List<QuestObjectiveData> Objectives,
    List<QuestRewardData> Rewards,
    List<Guid> ParticipantIds
) : ICommand;

/// <summary>
/// Обновить прогресс цели квеста.
/// </summary>
public record UpdateQuestObjectiveCommand(
    Guid CampaignId,
    Guid QuestId,
    int ObjectiveIndex,
    bool IsCompleted,
    int CurrentProgress
) : ICommand;

/// <summary>
/// Принять квест (начать выполнение).
/// </summary>
public record AcceptQuestCommand(
    Guid CampaignId,
    Guid QuestId
) : ICommand;

/// <summary>
/// Завершить квест успешно.
/// </summary>
public record CompleteQuestCommand(
    Guid CampaignId,
    Guid QuestId
) : ICommand;

/// <summary>
/// Провалить квест.
/// </summary>
public record FailQuestCommand(
    Guid CampaignId,
    Guid QuestId
) : ICommand;

/// <summary>
/// Удалить квест (административное действие).
/// </summary>
public record DeleteQuestCommand(
    Guid CampaignId,
    Guid QuestId
) : ICommand;

// ---------- Глобальные флаги ----------

public record SetGlobalFlagCommand(
    Guid CampaignId,
    string FlagName,
    string FlagValue
) : ICommand;

public record RemoveGlobalFlagCommand(
    Guid CampaignId,
    string FlagName
) : ICommand;

// ---------- Игровое время и погода ----------

public record AdvanceTimeCommand(
    Guid CampaignId,
    int Minutes
) : ICommand;

public record ChangeWeatherCommand(
    Guid CampaignId,
    string NewWeather
) : ICommand;

// ---------- Фракции ----------

public record ChangeFactionReputation(
    Guid CampaignId,
    string FactionId,
    int Change
) : ICommand;

// ---------- Дополнительные типы данных (если не определены в другом месте) ----------
// Эти типы уже есть в domain/events/quest_events.cs, но для удобства дублируем,
// чтобы файл был самодостаточным. В реальном проекте они должны быть в одном месте.
// Если они уже объявлены, закомментируйте или удалите дублирование.

// public record QuestObjectiveData
// {
//     public string Description { get; set; } = string.Empty;
//     public bool IsCompleted { get; set; }
//     public int CurrentProgress { get; set; }
//     public int RequiredProgress { get; set; }
// }
// 
// public record QuestRewardData
// {
//     public string Description { get; set; } = string.Empty;
//     public int ExperiencePoints { get; set; }
//     public List<string> ItemIds { get; set; } = new();
//     public int Gold { get; set; }
//     public string? FactionReputationChange { get; set; }
// }