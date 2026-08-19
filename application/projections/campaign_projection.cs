// application/projections/campaign_projection.cs
using dnd_game.Domain.Events;
using dnd_game.Infrastructure.Caching;
using System.Collections.Concurrent;

namespace dnd_game.Application.Projections
{
    /// <summary>
    /// Детальная информация о квесте.
    /// </summary>
    public class QuestInfo
    {
        public Guid QuestId { get; set; }
        public Guid CampaignId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public QuestStatus Status { get; set; } = QuestStatus.Active;
        public List<QuestObjective> Objectives { get; set; } = new();
        public List<QuestReward> Rewards { get; set; } = new();
        public DateTime IssuedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public enum QuestStatus
    {
        Active,
        Completed,
        Failed,
        OnHold
    }

    public class QuestObjective
    {
        public string Description { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public int CurrentProgress { get; set; }
        public int RequiredProgress { get; set; }
    }

    public class QuestReward
    {
        public string Description { get; set; } = string.Empty;
        public int ExperiencePoints { get; set; }
        public List<string> ItemIds { get; set; } = new();
        public int Gold { get; set; }
        public string? FactionReputationChange { get; set; }
    }

    /// <summary>
    /// Состояние фракции и отношение к партии.
    /// </summary>
    public class FactionState
    {
        public string FactionId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Reputation { get; set; } // от -100 (вражда) до +100 (союз)
        public string Attitude => Reputation switch
        {
            <= -75 => "Враждебное",
            <= -25 => "Недружелюбное",
            < 25 => "Нейтральное",
            < 75 => "Дружелюбное",
            _ => "Союзное"
        };
    }

    /// <summary>
    /// Полное состояние кампании.
    /// </summary>
    public class CampaignState
    {
        public Guid CampaignId { get; set; }
        public string CampaignName { get; set; } = string.Empty;
        public int CurrentAct { get; set; } = 1;
        public int Day { get; set; } = 1;
        public int Hour { get; set; } = 8;
        public int Minute { get; set; }
        public string Weather { get; set; } = "Ясно";
        public List<string> DiscoveredRegions { get; set; } = new();
        public Dictionary<string, string> GlobalFlags { get; set; } = new();
    }

    public class CampaignProjection
    {
        // Хранилища данных (в реальном приложении – БД)
        private readonly ConcurrentDictionary<Guid, List<QuestInfo>> _campaignQuests = new();
        private readonly ConcurrentDictionary<Guid, CampaignState> _campaignStates = new();
        private readonly ConcurrentDictionary<string, FactionState> _factions = new();
        private readonly ConcurrentDictionary<Guid, List<string>> _activeWorldEvents = new();

        private readonly ICacheProvider _cache;
        private readonly TimeSpan _cacheTtl;

        public CampaignProjection(ICacheProvider cache, TimeSpan? cacheTtl = null)
        {
            _cache = cache;
            _cacheTtl = cacheTtl ?? TimeSpan.FromMinutes(10);
        }

        private void InvalidateCache(Guid campaignId)
        {
            _ = Task.Run(async () =>
            {
                await _cache.RemoveAsync($"campaign:{campaignId}");
                await _cache.RemoveAsync($"campaign:quests:{campaignId}");
                await _cache.RemoveAsync($"campaign:activeQuests:{campaignId}");
            });
        }

        private void InvalidateFactionCache()
        {
            _ = Task.Run(async () =>
            {
                await _cache.RemoveAsync($"campaign:factions:all");
            });
        }

        // ---------- Обработка событий квестов ----------
        public void Apply(QuestCreated e)
        {
            var quests = _campaignQuests.GetOrAdd(e.CampaignId, _ => new List<QuestInfo>());
            quests.Add(new QuestInfo
            {
                QuestId = e.QuestId,
                CampaignId = e.CampaignId,
                Title = e.Title,
                Description = e.Description,
                Objectives = e.Objectives.Select(o => new QuestObjective
                {
                    Description = o.Description,
                    RequiredProgress = o.RequiredProgress
                }).ToList(),
                Rewards = e.Rewards.Select(r => new QuestReward
                {
                    Description = r.Description,
                    ExperiencePoints = r.ExperiencePoints,
                    ItemIds = r.ItemIds,
                    Gold = r.Gold,
                    FactionReputationChange = r.FactionReputationChange
                }).ToList(),
                IssuedAt = DateTime.UtcNow
            });
            InvalidateCache(e.CampaignId);
        }

        public void Apply(QuestAccepted e)
        {
            var quests = _campaignQuests.GetOrAdd(e.CampaignId, _ => new List<QuestInfo>());
            var quest = quests.FirstOrDefault(q => q.QuestId == e.QuestId);
            if (quest != null)
            {
                quest.Status = QuestStatus.Active;
                InvalidateCache(e.CampaignId);
            }
        }

        public void Apply(QuestCompleted e)
        {
            if (_campaignQuests.TryGetValue(e.CampaignId, out var quests))
            {
                var quest = quests.FirstOrDefault(q => q.QuestId == e.QuestId);
                if (quest != null)
                {
                    quest.Status = QuestStatus.Completed;
                    quest.CompletedAt = DateTime.UtcNow;
                    InvalidateCache(e.CampaignId);
                }
            }
        }

        public void Apply(QuestFailed e)
        {
            if (_campaignQuests.TryGetValue(e.CampaignId, out var quests))
            {
                var quest = quests.FirstOrDefault(q => q.QuestId == e.QuestId);
                if (quest != null)
                {
                    quest.Status = QuestStatus.Failed;
                    InvalidateCache(e.CampaignId);
                }
            }
        }

        public void Apply(QuestObjectiveUpdated e)
        {
            if (_campaignQuests.TryGetValue(e.CampaignId, out var quests))
            {
                var quest = quests.FirstOrDefault(q => q.QuestId == e.QuestId);
                var objective = quest?.Objectives.ElementAtOrDefault(e.ObjectiveIndex);
                if (objective != null)
                {
                    objective.IsCompleted = e.IsCompleted;
                    objective.CurrentProgress = e.CurrentProgress;
                    InvalidateCache(e.CampaignId);
                }
            }
        }

        public void Apply(QuestRewardClaimed e)
        {
            // можно пометить награду как выданную, если требуется отслеживание
        }

        // ---------- Обработка событий фракций ----------
        public void Apply(FactionDiscovered e)
        {
            _factions.GetOrAdd(e.FactionId, _ => new FactionState
            {
                FactionId = e.FactionId,
                Name = e.FactionName,
                Reputation = 0
            });
            InvalidateFactionCache();
        }

        public void Apply(FactionReputationChanged e)
        {
            if (_factions.TryGetValue(e.FactionId, out var faction))
            {
                faction.Reputation = Math.Clamp(faction.Reputation + e.Change, -100, 100);
                InvalidateFactionCache();
            }
        }

        // ---------- Обработка глобального состояния мира ----------
        public void Apply(CampaignCreated e)
        {
            _campaignStates.TryAdd(e.CampaignId, new CampaignState
            {
                CampaignId = e.CampaignId,
                CampaignName = e.Name
            });
            InvalidateCache(e.CampaignId);
        }

        public void Apply(GameTimeAdvanced e)
        {
            if (_campaignStates.TryGetValue(e.CampaignId, out var state))
            {
                state.Minute += e.Minutes;
                while (state.Minute >= 60) { state.Minute -= 60; state.Hour++; }
                while (state.Hour >= 24) { state.Hour -= 24; state.Day++; }
                InvalidateCache(e.CampaignId);
            }
        }

        public void Apply(WeatherChanged e)
        {
            if (_campaignStates.TryGetValue(e.CampaignId, out var state))
            {
                state.Weather = e.NewWeather;
                InvalidateCache(e.CampaignId);
            }
        }

        public void Apply(RegionDiscovered e)
        {
            if (_campaignStates.TryGetValue(e.CampaignId, out var state))
            {
                if (!state.DiscoveredRegions.Contains(e.RegionName))
                    state.DiscoveredRegions.Add(e.RegionName);
                InvalidateCache(e.CampaignId);
            }
        }

        public void Apply(GlobalFlagSet e)
        {
            if (_campaignStates.TryGetValue(e.CampaignId, out var state))
            {
                state.GlobalFlags[e.FlagName] = e.FlagValue;
                InvalidateCache(e.CampaignId);
            }
        }

        public void Apply(GlobalFlagRemoved e)
        {
            if (_campaignStates.TryGetValue(e.CampaignId, out var state))
            {
                state.GlobalFlags.Remove(e.FlagName);
                InvalidateCache(e.CampaignId);
            }
        }

        public void Apply(WorldEventTriggered e)
        {
            var events = _activeWorldEvents.GetOrAdd(e.CampaignId, _ => new List<string>());
            if (!events.Contains(e.EventName))
                events.Add(e.EventName);
            InvalidateCache(e.CampaignId);
        }

        // ---------- Методы чтения проекции (с кешем) ----------
        public async Task<List<Guid>> GetActiveQuestIds(Guid campaignId)
        {
            var cacheKey = $"campaign:activeQuests:{campaignId}";
            var cached = await _cache.GetAsync<List<Guid>>(cacheKey);
            if (cached != null)
                return cached;

            if (_campaignQuests.TryGetValue(campaignId, out var quests))
            {
                var result = quests.Where(q => q.Status == QuestStatus.Active).Select(q => q.QuestId).ToList();
                await _cache.SetAsync(cacheKey, result, _cacheTtl);
                return result;
            }
            return new List<Guid>();
        }

        public async Task<List<QuestInfo>> GetQuests(Guid campaignId, QuestStatus? statusFilter = null)
        {
            var cacheKey = $"campaign:quests:{campaignId}";
            // Не кешируем с фильтром, только все квесты
            if (!statusFilter.HasValue)
            {
                var cached = await _cache.GetAsync<List<QuestInfo>>(cacheKey);
                if (cached != null)
                    return cached;
            }

            if (_campaignQuests.TryGetValue(campaignId, out var quests))
            {
                var filtered = statusFilter.HasValue ? quests.Where(q => q.Status == statusFilter.Value).ToList() : quests;
                if (!statusFilter.HasValue)
                    await _cache.SetAsync(cacheKey, filtered, _cacheTtl);
                return filtered;
            }
            return new List<QuestInfo>();
        }

        public async Task<QuestInfo?> GetQuestDetails(Guid campaignId, Guid questId)
        {
            // Кешируем отдельные квесты? Можно не кешировать, или кешировать с ключом
            // Но для простоты оставим без кеша, т.к. этот метод используется редко.
            if (_campaignQuests.TryGetValue(campaignId, out var quests))
                return quests.FirstOrDefault(q => q.QuestId == questId);
            return null;
        }

        public async Task<CampaignState?> GetCampaignState(Guid campaignId)
        {
            var cacheKey = $"campaign:{campaignId}";
            var cached = await _cache.GetAsync<CampaignState>(cacheKey);
            if (cached != null)
                return cached;

            _campaignStates.TryGetValue(campaignId, out var state);
            if (state != null)
                await _cache.SetAsync(cacheKey, state, _cacheTtl);
            return state;
        }

        public async Task<FactionState?> GetFaction(string factionId)
        {
            var cacheKey = $"campaign:faction:{factionId}";
            var cached = await _cache.GetAsync<FactionState>(cacheKey);
            if (cached != null)
                return cached;

            _factions.TryGetValue(factionId, out var faction);
            if (faction != null)
                await _cache.SetAsync(cacheKey, faction, _cacheTtl);
            return faction;
        }

        public async Task<List<FactionState>> GetAllFactions()
        {
            const string cacheKey = "campaign:factions:all";
            var cached = await _cache.GetAsync<List<FactionState>>(cacheKey);
            if (cached != null)
                return cached;

            var list = _factions.Values.ToList();
            await _cache.SetAsync(cacheKey, list, _cacheTtl);
            return list;
        }

        public async Task<List<string>> GetActiveWorldEvents(Guid campaignId)
        {
            var cacheKey = $"campaign:worldEvents:{campaignId}";
            var cached = await _cache.GetAsync<List<string>>(cacheKey);
            if (cached != null)
                return cached;

            if (_activeWorldEvents.TryGetValue(campaignId, out var events))
            {
                await _cache.SetAsync(cacheKey, events, _cacheTtl);
                return events;
            }
            return new List<string>();
        }
    }
}