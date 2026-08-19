// domain/aggregates/campaign_aggregate.cs
using dnd_game.Domain.Events;

namespace dnd_game.Domain.Aggregates
{
    public class CampaignAggregate : AggregateRoot
    {
        // ---------- Поля состояния ----------
        public string Name { get; private set; } = string.Empty;
        public Guid GameMasterId { get; private set; }
        public List<Guid> PlayerIds { get; private set; } = [];
        public List<Guid> ActiveQuestIds { get; private set; } = [];
        public Dictionary<string, int> FactionReputations { get; private set; } = []; // FactionId -> Reputation (-100..100)
        public Dictionary<string, string> GlobalFlags { get; private set; } = [];      // FlagName -> Value
        public int Day { get; private set; } = 1;
        public int Hour { get; private set; } = 8;
        public int Minute { get; private set; } = 0;
        public string CurrentWeather { get; private set; } = "Ясно";
        public List<string> DiscoveredRegions { get; private set; } = [];
        public List<CampaignQuestInfo> Quests { get; private set; } = [];              // детали квестов

        // ---------- Конструкторы ----------
        public CampaignAggregate(Guid campaignId, string name, Guid gameMasterId)
        {
            ApplyChange(new CampaignCreated(campaignId, name, gameMasterId, DateTime.UtcNow));
        }

        // Параметрless конструктор для event sourcing
        public CampaignAggregate() { }

        // ---------- Применение событий ----------
        protected override void ApplyEvent(IDomainEvent @event)
        {
            switch (@event)
            {
                case CampaignCreated e:
                    Id = e.CampaignId;
                    Name = e.Name;
                    GameMasterId = e.GameMasterId;
                    break;

                // --- Игроки ---
                case PlayerJoinedCampaign e:
                    if (!PlayerIds.Contains(e.PlayerId))
                        PlayerIds.Add(e.PlayerId);
                    break;
                case PlayerLeftCampaign e:
                    PlayerIds.Remove(e.PlayerId);
                    break;

                // --- Квесты ---
                case QuestAccepted e:
                    if (!ActiveQuestIds.Contains(e.QuestId))
                        ActiveQuestIds.Add(e.QuestId);
                    var questInfo = Quests.FirstOrDefault(q => q.QuestId == e.QuestId);
                    if (questInfo != null)
                        questInfo.Status = QuestStatus.Active;
                    // Дополнительно можно сохранить ParticipantIds в состояние квеста (если нужно)
                    break;
                case QuestCompleted e:
                    ActiveQuestIds.Remove(e.QuestId);
                    var qComp = Quests.FirstOrDefault(q => q.QuestId == e.QuestId);
                    if (qComp != null)
                    {
                        qComp.Status = QuestStatus.Completed;
                        qComp.CompletedAt = e.Timestamp; // или e.Timestamp, смотря как названо поле
                    }
                    break;
                case QuestFailed e:
                    ActiveQuestIds.Remove(e.QuestId);
                    var qFail = Quests.FirstOrDefault(q => q.QuestId == e.QuestId);
                    if (qFail != null)
                        qFail.Status = QuestStatus.Failed;
                    break;
                case QuestCreated e:
                    Quests.Add(new CampaignQuestInfo
                    {
                        QuestId = e.QuestId,
                        Title = e.Title,
                        Status = QuestStatus.Available,
                        Objectives = e.Objectives,
                        Rewards = e.Rewards,
                        IssuedAt = e.IssuedAt,
                        // Можно также сохранить ParticipantIds, если нужно в агрегате
                    });
                    break;
                case QuestObjectiveUpdated e:
                    var quest = Quests.FirstOrDefault(q => q.QuestId == e.QuestId);
                    var obj = quest?.Objectives.ElementAtOrDefault(e.ObjectiveIndex);
                    if (obj != null)
                    {
                        obj.IsCompleted = e.IsCompleted;
                        obj.CurrentProgress = e.CurrentProgress;
                    }
                    break;

                // --- Фракции ---
                case FactionAdded e:
                    if (!FactionReputations.ContainsKey(e.FactionId))
                        FactionReputations[e.FactionId] = e.InitialReputation;
                    break;
                case FactionReputationChanged e:
                    if (FactionReputations.TryGetValue(e.FactionId, out int value))
                    {
                        FactionReputations[e.FactionId] = Math.Clamp(
value + e.Change, -100, 100);
                    }
                    break;

                // --- Глобальные флаги ---
                case GlobalFlagSet e:
                    GlobalFlags[e.FlagName] = e.FlagValue;
                    break;
                case GlobalFlagRemoved e:
                    GlobalFlags.Remove(e.FlagName);
                    break;

                // --- Игровое время ---
                case GameTimeAdvanced e:
                    Minute += e.Minutes;
                    while (Minute >= 60) { Minute -= 60; Hour++; }
                    while (Hour >= 24) { Hour -= 24; Day++; }
                    break;
                case WeatherChanged e:
                    CurrentWeather = e.NewWeather;
                    break;

                // --- Регионы ---
                case RegionDiscovered e:
                    if (!DiscoveredRegions.Contains(e.RegionName))
                        DiscoveredRegions.Add(e.RegionName);
                    break;
            }
        }

        // ---------- Команды (методы, порождающие события) ----------

        // Управление игроками
        public void JoinPlayer(Guid playerId)
        {
            if (PlayerIds.Contains(playerId))
                throw new InvalidOperationException("Player already in campaign");
            ApplyChange(new PlayerJoinedCampaign(Id, playerId, DateTime.UtcNow));
        }

        public void LeavePlayer(Guid playerId)
        {
            if (!PlayerIds.Contains(playerId))
                throw new InvalidOperationException("Player not in campaign");
            ApplyChange(new PlayerLeftCampaign(Id, playerId, DateTime.UtcNow));
        }

        // Квесты
        public void AcceptQuest(Guid questId)
        {
            if (ActiveQuestIds.Contains(questId))
                throw new InvalidOperationException("Quest already active");
            var quest = Quests.FirstOrDefault(q => q.QuestId == questId) ?? throw new InvalidOperationException("Quest not found in campaign");
            ApplyChange(new QuestAccepted(Id, questId, new List<Guid>(), DateTime.UtcNow));
        }

        public void CompleteQuest(Guid questId)
        {
            if (!ActiveQuestIds.Contains(questId))
                throw new InvalidOperationException("Quest not active");
            ApplyChange(new QuestCompleted(Id, questId, DateTime.UtcNow));
        }

        public void FailQuest(Guid questId)
        {
            if (!ActiveQuestIds.Contains(questId))
                throw new InvalidOperationException("Quest not active");
            ApplyChange(new QuestFailed(Id, questId, DateTime.UtcNow));
        }

        public void CreateQuest(Guid questId, string title, List<QuestObjectiveData> objectives,
            List<QuestRewardData> rewards, List<Guid> participantIds)   // новый параметр
        {
            if (Quests.Any(q => q.QuestId == questId))
                throw new InvalidOperationException("Quest already exists");
            ApplyChange(new QuestCreated(Id, questId, title, "", objectives, rewards, participantIds, DateTime.UtcNow));
        }

        public void UpdateQuestObjective(Guid questId, int objectiveIndex, bool isCompleted, int currentProgress)
        {
            var quest = Quests.FirstOrDefault(q => q.QuestId == questId) ?? throw new InvalidOperationException("Quest not found");
            if (objectiveIndex < 0 || objectiveIndex >= quest.Objectives.Count)
                throw new InvalidOperationException("Invalid objective index");
            ApplyChange(new QuestObjectiveUpdated(Id, questId, objectiveIndex, isCompleted, currentProgress));
        }

        // Фракции
        public void AddFaction(string factionId, int initialReputation = 0)
        {
            if (FactionReputations.ContainsKey(factionId))
                throw new InvalidOperationException("Faction already exists in campaign");
            ApplyChange(new FactionAdded(Id, factionId, initialReputation));
        }

        public void ChangeFactionReputation(string factionId, int change)
        {
            if (!FactionReputations.ContainsKey(factionId))
                throw new InvalidOperationException("Faction not found");
            ApplyChange(new FactionReputationChanged(Id, factionId, change));
        }

        // Глобальные флаги
        public void SetGlobalFlag(string flagName, string value)
        {
            ApplyChange(new GlobalFlagSet(Id, flagName, value));
        }

        public void RemoveGlobalFlag(string flagName)
        {
            if (!GlobalFlags.ContainsKey(flagName))
                throw new InvalidOperationException("Flag not found");
            ApplyChange(new GlobalFlagRemoved(Id, flagName));
        }

        // Время и погода
        public void AdvanceTime(int minutes)
        {
            if (minutes <= 0) throw new ArgumentException("Minutes must be positive");
            ApplyChange(new GameTimeAdvanced(Id, minutes));
        }

        public void ChangeWeather(string newWeather)
        {
            if (string.IsNullOrWhiteSpace(newWeather)) throw new ArgumentException("Weather cannot be empty");
            ApplyChange(new WeatherChanged(Id, newWeather));
        }

        // Исследование
        public void DiscoverRegion(string regionName)
        {
            if (DiscoveredRegions.Contains(regionName))
                return; // уже открыто, не дублируем событие
            ApplyChange(new RegionDiscovered(Id, regionName));
        }
    }

    // Вспомогательные типы для внутреннего состояния квестов
    public enum QuestStatus
    {
        Available,
        Active,
        Completed,
        Failed
    }

    public class CampaignQuestInfo
    {
        public Guid QuestId { get; set; }
        public string Title { get; set; } = string.Empty;
        public QuestStatus Status { get; set; } = QuestStatus.Available;
        public List<QuestObjectiveData> Objectives { get; set; } = [];
        public List<QuestRewardData> Rewards { get; set; } = [];
        public DateTime IssuedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}