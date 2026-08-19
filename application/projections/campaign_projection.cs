// application/projections/campaign_projection.cs
using dnd_game.Domain.Events;
using dnd_game.Infrastructure.Caching;
using System.Collections.Concurrent;

namespace dnd_game.Application.Projections
{
    /// <summary>
    /// Детальная информация о квесте, используемая в проекции кампании.
    /// Содержит все данные, необходимые для отображения и отслеживания квеста.
    /// </summary>
    public class QuestInfo
    {
        /// <summary>Уникальный идентификатор квеста.</summary>
        public Guid QuestId { get; set; }

        /// <summary>Идентификатор кампании, к которой относится квест.</summary>
        public Guid CampaignId { get; set; }

        /// <summary>Название квеста.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Описание квеста.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Текущий статус квеста.</summary>
        public QuestStatus Status { get; set; } = QuestStatus.Active;

        /// <summary>Список целей квеста.</summary>
        public List<QuestObjective> Objectives { get; set; } = new();

        /// <summary>Список наград за выполнение квеста.</summary>
        public List<QuestReward> Rewards { get; set; } = new();

        /// <summary>Дата и время выдачи квеста (UTC).</summary>
        public DateTime IssuedAt { get; set; }

        /// <summary>Дата и время завершения квеста (если выполнен).</summary>
        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>
    /// Статус квеста.
    /// </summary>
    public enum QuestStatus
    {
        /// <summary>Квест активен и выполняется.</summary>
        Active,

        /// <summary>Квест успешно завершён.</summary>
        Completed,

        /// <summary>Квест провален.</summary>
        Failed,

        /// <summary>Квест временно приостановлен.</summary>
        OnHold
    }

    /// <summary>
    /// Цель (задача) квеста, которую необходимо выполнить.
    /// </summary>
    public class QuestObjective
    {
        /// <summary>Описание цели.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Выполнена ли цель.</summary>
        public bool IsCompleted { get; set; }

        /// <summary>Текущий прогресс выполнения (например, количество убитых монстров).</summary>
        public int CurrentProgress { get; set; }

        /// <summary>Необходимый прогресс для завершения цели.</summary>
        public int RequiredProgress { get; set; }
    }

    /// <summary>
    /// Награда за выполнение квеста.
    /// </summary>
    public class QuestReward
    {
        /// <summary>Описание награды.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Количество опыта, получаемое за квест.</summary>
        public int ExperiencePoints { get; set; }

        /// <summary>Список идентификаторов предметов, выдаваемых в качестве награды.</summary>
        public List<string> ItemIds { get; set; } = new();

        /// <summary>Количество золота, получаемое за квест.</summary>
        public int Gold { get; set; }

        /// <summary>Изменение репутации с фракцией (если применимо).</summary>
        public string? FactionReputationChange { get; set; }
    }

    /// <summary>
    /// Состояние фракции и отношение к партии.
    /// </summary>
    public class FactionState
    {
        /// <summary>Идентификатор фракции.</summary>
        public string FactionId { get; set; } = string.Empty;

        /// <summary>Название фракции.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Репутация от -100 (вражда) до +100 (союз).</summary>
        public int Reputation { get; set; }

        /// <summary>
        /// Текстовое описание отношения фракции к партии, вычисляемое на основе репутации.
        /// </summary>
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
    /// Полное состояние кампании (глобальные данные мира, время, погода, открытые регионы).
    /// </summary>
    public class CampaignState
    {
        /// <summary>Идентификатор кампании.</summary>
        public Guid CampaignId { get; set; }

        /// <summary>Название кампании.</summary>
        public string CampaignName { get; set; } = string.Empty;

        /// <summary>Текущий акт кампании.</summary>
        public int CurrentAct { get; set; } = 1;

        /// <summary>Текущий игровой день.</summary>
        public int Day { get; set; } = 1;

        /// <summary>Текущий игровой час.</summary>
        public int Hour { get; set; } = 8;

        /// <summary>Текущая игровая минута.</summary>
        public int Minute { get; set; }

        /// <summary>Текущая погода (текстовое описание).</summary>
        public string Weather { get; set; } = "Ясно";

        /// <summary>Список открытых регионов.</summary>
        public List<string> DiscoveredRegions { get; set; } = new();

        /// <summary>Глобальные флаги кампании (ключ-значение).</summary>
        public Dictionary<string, string> GlobalFlags { get; set; } = new();
    }

    /// <summary>
    /// Проекция кампании, отвечающая за построение read-модели на основе событий предметной области.
    /// Хранит данные в памяти (в реальном приложении — в базе данных) и предоставляет методы чтения с кешированием.
    /// </summary>
    public class CampaignProjection
    {
        // ---------- Хранилища данных (в реальном приложении – БД) ----------
        // Каждое из этих хранилищ содержит часть состояния, необходимую для проекции.

        /// <summary>Сопоставление идентификатора кампании со списком её квестов.</summary>
        private readonly ConcurrentDictionary<Guid, List<QuestInfo>> _campaignQuests = new();

        /// <summary>Сопоставление идентификатора кампании с её глобальным состоянием.</summary>
        private readonly ConcurrentDictionary<Guid, CampaignState> _campaignStates = new();

        /// <summary>Сопоставление идентификатора фракции с её состоянием.</summary>
        private readonly ConcurrentDictionary<string, FactionState> _factions = new();

        /// <summary>Сопоставление идентификатора кампании со списком активных мировых событий.</summary>
        private readonly ConcurrentDictionary<Guid, List<string>> _activeWorldEvents = new();

        /// <summary>Провайдер кеша для ускорения операций чтения.</summary>
        private readonly ICacheProvider _cache;

        /// <summary>Время жизни записей в кеше.</summary>
        private readonly TimeSpan _cacheTtl;

        /// <summary>
        /// Инициализирует новый экземпляр проекции кампании.
        /// </summary>
        /// <param name="cache">Провайдер кеша.</param>
        /// <param name="cacheTtl">Время жизни кеша; по умолчанию 10 минут.</param>
        public CampaignProjection(ICacheProvider cache, TimeSpan? cacheTtl = null)
        {
            _cache = cache;
            _cacheTtl = cacheTtl ?? TimeSpan.FromMinutes(10);
        }

        /// <summary>
        /// Инвалидирует кеш, связанный с конкретной кампанией.
        /// Запускается в фоновом потоке, чтобы не блокировать обработку события.
        /// </summary>
        /// <param name="campaignId">Идентификатор кампании, кеш которой нужно сбросить.</param>
        private void InvalidateCache(Guid campaignId)
        {
            _ = Task.Run(async () =>
            {
                await _cache.RemoveAsync($"campaign:{campaignId}");
                await _cache.RemoveAsync($"campaign:quests:{campaignId}");
                await _cache.RemoveAsync($"campaign:activeQuests:{campaignId}");
            });
        }

        /// <summary>
        /// Инвалидирует кеш, связанный со списком всех фракций.
        /// </summary>
        private void InvalidateFactionCache()
        {
            _ = Task.Run(async () =>
            {
                await _cache.RemoveAsync($"campaign:factions:all");
            });
        }

        // ---------- Обработка событий квестов ----------

        /// <summary>
        /// Обрабатывает событие создания квеста (<see cref="QuestCreated"/>).
        /// Добавляет новый квест в хранилище и сбрасывает кеш кампании.
        /// </summary>
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

        /// <summary>
        /// Обрабатывает событие принятия квеста (<see cref="QuestAccepted"/>).
        /// Обновляет статус квеста на активный.
        /// </summary>
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

        /// <summary>
        /// Обрабатывает событие завершения квеста (<see cref="QuestCompleted"/>).
        /// Устанавливает статус "Завершён" и фиксирует время завершения.
        /// </summary>
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

        /// <summary>
        /// Обрабатывает событие провала квеста (<see cref="QuestFailed"/>).
        /// Устанавливает статус "Провален".
        /// </summary>
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

        /// <summary>
        /// Обрабатывает событие обновления цели квеста (<see cref="QuestObjectiveUpdated"/>).
        /// Обновляет прогресс и статус конкретной цели.
        /// </summary>
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

        /// <summary>
        /// Обрабатывает событие получения награды за квест (<see cref="QuestRewardClaimed"/>).
        /// В текущей реализации не выполняет действий, но может быть расширено для отслеживания выданных наград.
        /// </summary>
        public void Apply(QuestRewardClaimed e)
        {
            // можно пометить награду как выданную, если требуется отслеживание
        }

        // ---------- Обработка событий фракций ----------

        /// <summary>
        /// Обрабатывает событие обнаружения фракции (<see cref="FactionDiscovered"/>).
        /// Добавляет новую фракцию с нулевой репутацией, если её ещё нет.
        /// </summary>
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

        /// <summary>
        /// Обрабатывает событие изменения репутации с фракцией (<see cref="FactionReputationChanged"/>).
        /// Обновляет репутацию, ограничивая её диапазоном [-100, 100].
        /// </summary>
        public void Apply(FactionReputationChanged e)
        {
            if (_factions.TryGetValue(e.FactionId, out var faction))
            {
                faction.Reputation = Math.Clamp(faction.Reputation + e.Change, -100, 100);
                InvalidateFactionCache();
            }
        }

        // ---------- Обработка глобального состояния мира ----------

        /// <summary>
        /// Обрабатывает событие создания кампании (<see cref="CampaignCreated"/>).
        /// Инициализирует состояние кампании.
        /// </summary>
        public void Apply(CampaignCreated e)
        {
            _campaignStates.TryAdd(e.CampaignId, new CampaignState
            {
                CampaignId = e.CampaignId,
                CampaignName = e.Name
            });
            InvalidateCache(e.CampaignId);
        }

        /// <summary>
        /// Обрабатывает событие продвижения игрового времени (<see cref="GameTimeAdvanced"/>).
        /// Обновляет минуты, часы и дни с учётом переноса.
        /// </summary>
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

        /// <summary>
        /// Обрабатывает событие смены погоды (<see cref="WeatherChanged"/>).
        /// </summary>
        public void Apply(WeatherChanged e)
        {
            if (_campaignStates.TryGetValue(e.CampaignId, out var state))
            {
                state.Weather = e.NewWeather;
                InvalidateCache(e.CampaignId);
            }
        }

        /// <summary>
        /// Обрабатывает событие открытия региона (<see cref="RegionDiscovered"/>).
        /// Добавляет регион в список открытых, если его там ещё нет.
        /// </summary>
        public void Apply(RegionDiscovered e)
        {
            if (_campaignStates.TryGetValue(e.CampaignId, out var state))
            {
                if (!state.DiscoveredRegions.Contains(e.RegionName))
                    state.DiscoveredRegions.Add(e.RegionName);
                InvalidateCache(e.CampaignId);
            }
        }

        /// <summary>
        /// Обрабатывает событие установки глобального флага (<see cref="GlobalFlagSet"/>).
        /// Сохраняет или обновляет флаг в состоянии кампании.
        /// </summary>
        public void Apply(GlobalFlagSet e)
        {
            if (_campaignStates.TryGetValue(e.CampaignId, out var state))
            {
                state.GlobalFlags[e.FlagName] = e.FlagValue;
                InvalidateCache(e.CampaignId);
            }
        }

        /// <summary>
        /// Обрабатывает событие удаления глобального флага (<see cref="GlobalFlagRemoved"/>).
        /// </summary>
        public void Apply(GlobalFlagRemoved e)
        {
            if (_campaignStates.TryGetValue(e.CampaignId, out var state))
            {
                state.GlobalFlags.Remove(e.FlagName);
                InvalidateCache(e.CampaignId);
            }
        }

        /// <summary>
        /// Обрабатывает событие активации мирового события (<see cref="WorldEventTriggered"/>).
        /// Добавляет событие в список активных, если его там нет.
        /// </summary>
        public void Apply(WorldEventTriggered e)
        {
            var events = _activeWorldEvents.GetOrAdd(e.CampaignId, _ => new List<string>());
            if (!events.Contains(e.EventName))
                events.Add(e.EventName);
            InvalidateCache(e.CampaignId);
        }

        // ---------- Методы чтения проекции (с кешем) ----------

        /// <summary>
        /// Получает список идентификаторов активных квестов кампании.
        /// Использует кеширование для ускорения повторных запросов.
        /// </summary>
        /// <param name="campaignId">Идентификатор кампании.</param>
        /// <returns>Список идентификаторов активных квестов.</returns>
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

        /// <summary>
        /// Получает список квестов кампании с возможностью фильтрации по статусу.
        /// Без фильтра результат кешируется; при наличии фильтра кеш не используется.
        /// </summary>
        /// <param name="campaignId">Идентификатор кампании.</param>
        /// <param name="statusFilter">Необязательный фильтр по статусу квеста.</param>
        /// <returns>Список квестов, соответствующих фильтру (или все, если фильтр отсутствует).</returns>
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

        /// <summary>
        /// Получает детальную информацию о конкретном квесте.
        /// Кеширование не применяется, так как метод используется редко.
        /// </summary>
        /// <param name="campaignId">Идентификатор кампании.</param>
        /// <param name="questId">Идентификатор квеста.</param>
        /// <returns>Объект <see cref="QuestInfo"/> или null, если квест не найден.</returns>
        public async Task<QuestInfo?> GetQuestDetails(Guid campaignId, Guid questId)
        {
            if (_campaignQuests.TryGetValue(campaignId, out var quests))
                return quests.FirstOrDefault(q => q.QuestId == questId);
            return null;
        }

        /// <summary>
        /// Получает глобальное состояние кампании.
        /// Использует кеширование для ускорения повторных запросов.
        /// </summary>
        /// <param name="campaignId">Идентификатор кампании.</param>
        /// <returns>Объект <see cref="CampaignState"/> или null, если кампания не найдена.</returns>
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

        /// <summary>
        /// Получает состояние фракции по её идентификатору.
        /// Использует кеширование.
        /// </summary>
        /// <param name="factionId">Идентификатор фракции.</param>
        /// <returns>Объект <see cref="FactionState"/> или null, если фракция не найдена.</returns>
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

        /// <summary>
        /// Получает список всех известных фракций.
        /// Использует кеширование.
        /// </summary>
        /// <returns>Список состояний фракций.</returns>
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

        /// <summary>
        /// Получает список активных мировых событий кампании.
        /// Использует кеширование.
        /// </summary>
        /// <param name="campaignId">Идентификатор кампании.</param>
        /// <returns>Список названий активных событий.</returns>
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