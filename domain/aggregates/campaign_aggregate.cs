// domain/aggregates/campaign_aggregate.cs
using dnd_game.Domain.Events;

namespace dnd_game.Domain.Aggregates
{
    /// <summary>
    /// Агрегат кампании. Управляет состоянием кампании: игроками, квестами, фракциями,
    /// глобальными флагами, временем, погодой и открытыми регионами.
    /// Реализует событийно-ориентированное восстановление состояния (event sourcing).
    /// </summary>
    public class CampaignAggregate : AggregateRoot
    {
        // ---------- Поля состояния ----------

        /// <summary>Название кампании.</summary>
        public string Name { get; private set; } = string.Empty;

        /// <summary>Идентификатор Мастера игры, ведущего кампанию.</summary>
        public Guid GameMasterId { get; private set; }

        /// <summary>Список идентификаторов игроков, участвующих в кампании.</summary>
        public List<Guid> PlayerIds { get; private set; } = [];

        /// <summary>Список идентификаторов активных (принятых) квестов.</summary>
        public List<Guid> ActiveQuestIds { get; private set; } = [];

        /// <summary>Репутации фракций: идентификатор фракции → значение (-100..100).</summary>
        public Dictionary<string, int> FactionReputations { get; private set; } = [];

        /// <summary>Глобальные флаги кампании: имя флага → значение.</summary>
        public Dictionary<string, string> GlobalFlags { get; private set; } = [];

        /// <summary>Текущий игровой день (начиная с 1).</summary>
        public int Day { get; private set; } = 1;

        /// <summary>Текущий игровой час (0-23).</summary>
        public int Hour { get; private set; } = 8;

        /// <summary>Текущая игровая минута (0-59).</summary>
        public int Minute { get; private set; } = 0;

        /// <summary>Текущая погода (текстовое описание).</summary>
        public string CurrentWeather { get; private set; } = "Ясно";

        /// <summary>Список открытых (исследованных) регионов.</summary>
        public List<string> DiscoveredRegions { get; private set; } = [];

        /// <summary>Список всех квестов кампании с детальной информацией.</summary>
        public List<CampaignQuestInfo> Quests { get; private set; } = [];

        // ---------- Конструкторы ----------

        /// <summary>
        /// Создаёт новый агрегат кампании, применяя событие <see cref="CampaignCreated"/>.
        /// </summary>
        /// <param name="campaignId">Идентификатор кампании.</param>
        /// <param name="name">Название кампании.</param>
        /// <param name="gameMasterId">Идентификатор Мастера игры.</param>
        public CampaignAggregate(Guid campaignId, string name, Guid gameMasterId)
        {
            ApplyChange(new CampaignCreated(campaignId, name, gameMasterId, DateTime.UtcNow));
        }

        /// <summary>
        /// Конструктор без параметров, используемый для восстановления агрегата из истории событий.
        /// </summary>
        public CampaignAggregate() { }

        // ---------- Применение событий ----------

        /// <summary>
        /// Применяет доменное событие к состоянию агрегата.
        /// Вызывается как при первоначальном создании, так и при загрузке из EventStore.
        /// </summary>
        /// <param name="event">Событие предметной области.</param>
        protected override void ApplyEvent(IDomainEvent @event)
        {
            switch (@event)
            {
                // Создание кампании
                case CampaignCreated e:
                    Id = e.CampaignId;
                    Name = e.Name;
                    GameMasterId = e.GameMasterId;
                    break;

                // --- Игроки ---

                // Присоединение игрока к кампании
                case PlayerJoinedCampaign e:
                    if (!PlayerIds.Contains(e.PlayerId))
                        PlayerIds.Add(e.PlayerId);
                    break;

                // Выход игрока из кампании
                case PlayerLeftCampaign e:
                    PlayerIds.Remove(e.PlayerId);
                    break;

                // --- Квесты ---

                // Принятие квеста: добавляем в активные и обновляем статус
                case QuestAccepted e:
                    if (!ActiveQuestIds.Contains(e.QuestId))
                        ActiveQuestIds.Add(e.QuestId);
                    var questInfo = Quests.FirstOrDefault(q => q.QuestId == e.QuestId);
                    if (questInfo != null)
                        questInfo.Status = QuestStatus.Active;
                    // При необходимости можно сохранить ParticipantIds в состояние квеста
                    break;

                // Завершение квеста: удаляем из активных, обновляем статус и время завершения
                case QuestCompleted e:
                    ActiveQuestIds.Remove(e.QuestId);
                    var qComp = Quests.FirstOrDefault(q => q.QuestId == e.QuestId);
                    if (qComp != null)
                    {
                        qComp.Status = QuestStatus.Completed;
                        qComp.CompletedAt = e.Timestamp;
                    }
                    break;

                // Провал квеста: удаляем из активных и обновляем статус
                case QuestFailed e:
                    ActiveQuestIds.Remove(e.QuestId);
                    var qFail = Quests.FirstOrDefault(q => q.QuestId == e.QuestId);
                    if (qFail != null)
                        qFail.Status = QuestStatus.Failed;
                    break;

                // Создание нового квеста
                case QuestCreated e:
                    Quests.Add(new CampaignQuestInfo
                    {
                        QuestId = e.QuestId,
                        Title = e.Title,
                        Status = QuestStatus.Available,
                        Objectives = e.Objectives,
                        Rewards = e.Rewards,
                        IssuedAt = e.IssuedAt,
                        // ParticipantIds можно сохранить при необходимости
                    });
                    break;

                // Обновление цели квеста
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

                // Добавление фракции в кампанию
                case FactionAdded e:
                    if (!FactionReputations.ContainsKey(e.FactionId))
                        FactionReputations[e.FactionId] = e.InitialReputation;
                    break;

                // Изменение репутации фракции (ограничено диапазоном -100..100)
                case FactionReputationChanged e:
                    if (FactionReputations.TryGetValue(e.FactionId, out int value))
                    {
                        FactionReputations[e.FactionId] = Math.Clamp(value + e.Change, -100, 100);
                    }
                    break;

                // --- Глобальные флаги ---

                // Установка глобального флага
                case GlobalFlagSet e:
                    GlobalFlags[e.FlagName] = e.FlagValue;
                    break;

                // Удаление глобального флага
                case GlobalFlagRemoved e:
                    GlobalFlags.Remove(e.FlagName);
                    break;

                // --- Игровое время ---

                // Продвижение игрового времени (минуты, часы, дни)
                case GameTimeAdvanced e:
                    Minute += e.Minutes;
                    while (Minute >= 60) { Minute -= 60; Hour++; }
                    while (Hour >= 24) { Hour -= 24; Day++; }
                    break;

                // Смена погоды
                case WeatherChanged e:
                    CurrentWeather = e.NewWeather;
                    break;

                // --- Регионы ---

                // Открытие нового региона (добавляем, если ещё не открыт)
                case RegionDiscovered e:
                    if (!DiscoveredRegions.Contains(e.RegionName))
                        DiscoveredRegions.Add(e.RegionName);
                    break;
            }
        }

        // ---------- Команды (методы, порождающие события) ----------

        /// <summary>
        /// Добавляет игрока в кампанию.
        /// </summary>
        /// <param name="playerId">Идентификатор игрока.</param>
        /// <exception cref="InvalidOperationException">Если игрок уже в кампании.</exception>
        public void JoinPlayer(Guid playerId)
        {
            if (PlayerIds.Contains(playerId))
                throw new InvalidOperationException("Player already in campaign");
            ApplyChange(new PlayerJoinedCampaign(Id, playerId, DateTime.UtcNow));
        }

        /// <summary>
        /// Удаляет игрока из кампании.
        /// </summary>
        /// <param name="playerId">Идентификатор игрока.</param>
        /// <exception cref="InvalidOperationException">Если игрок не состоит в кампании.</exception>
        public void LeavePlayer(Guid playerId)
        {
            if (!PlayerIds.Contains(playerId))
                throw new InvalidOperationException("Player not in campaign");
            ApplyChange(new PlayerLeftCampaign(Id, playerId, DateTime.UtcNow));
        }

        /// <summary>
        /// Принимает квест (делает его активным).
        /// </summary>
        /// <param name="questId">Идентификатор квеста.</param>
        /// <exception cref="InvalidOperationException">Если квест уже активен или не найден.</exception>
        public void AcceptQuest(Guid questId)
        {
            if (ActiveQuestIds.Contains(questId))
                throw new InvalidOperationException("Quest already active");
            var quest = Quests.FirstOrDefault(q => q.QuestId == questId)
                        ?? throw new InvalidOperationException("Quest not found in campaign");
            ApplyChange(new QuestAccepted(Id, questId, new List<Guid>(), DateTime.UtcNow));
        }

        /// <summary>
        /// Завершает активный квест.
        /// </summary>
        /// <param name="questId">Идентификатор квеста.</param>
        /// <exception cref="InvalidOperationException">Если квест не активен.</exception>
        public void CompleteQuest(Guid questId)
        {
            if (!ActiveQuestIds.Contains(questId))
                throw new InvalidOperationException("Quest not active");
            ApplyChange(new QuestCompleted(Id, questId, DateTime.UtcNow));
        }

        /// <summary>
        /// Проваливает активный квест.
        /// </summary>
        /// <param name="questId">Идентификатор квеста.</param>
        /// <exception cref="InvalidOperationException">Если квест не активен.</exception>
        public void FailQuest(Guid questId)
        {
            if (!ActiveQuestIds.Contains(questId))
                throw new InvalidOperationException("Quest not active");
            ApplyChange(new QuestFailed(Id, questId, DateTime.UtcNow));
        }

        /// <summary>
        /// Создаёт новый квест в кампании.
        /// </summary>
        /// <param name="questId">Идентификатор квеста.</param>
        /// <param name="title">Название квеста.</param>
        /// <param name="objectives">Список целей квеста.</param>
        /// <param name="rewards">Список наград.</param>
        /// <param name="participantIds">Список идентификаторов участников (персонажей), связанных с квестом.</param>
        /// <exception cref="InvalidOperationException">Если квест с таким идентификатором уже существует.</exception>
        public void CreateQuest(Guid questId, string title, List<QuestObjectiveData> objectives,
            List<QuestRewardData> rewards, List<Guid> participantIds)
        {
            if (Quests.Any(q => q.QuestId == questId))
                throw new InvalidOperationException("Quest already exists");
            ApplyChange(new QuestCreated(Id, questId, title, "", objectives, rewards, participantIds, DateTime.UtcNow));
        }

        /// <summary>
        /// Обновляет прогресс цели квеста.
        /// </summary>
        /// <param name="questId">Идентификатор квеста.</param>
        /// <param name="objectiveIndex">Индекс цели в списке.</param>
        /// <param name="isCompleted">Флаг завершения цели.</param>
        /// <param name="currentProgress">Текущий прогресс цели.</param>
        /// <exception cref="InvalidOperationException">Если квест не найден или индекс цели некорректен.</exception>
        public void UpdateQuestObjective(Guid questId, int objectiveIndex, bool isCompleted, int currentProgress)
        {
            var quest = Quests.FirstOrDefault(q => q.QuestId == questId)
                        ?? throw new InvalidOperationException("Quest not found");
            if (objectiveIndex < 0 || objectiveIndex >= quest.Objectives.Count)
                throw new InvalidOperationException("Invalid objective index");
            ApplyChange(new QuestObjectiveUpdated(Id, questId, objectiveIndex, isCompleted, currentProgress));
        }

        /// <summary>
        /// Добавляет фракцию в кампанию с начальной репутацией.
        /// </summary>
        /// <param name="factionId">Идентификатор фракции.</param>
        /// <param name="initialReputation">Начальная репутация (по умолчанию 0).</param>
        /// <exception cref="InvalidOperationException">Если фракция уже существует в кампании.</exception>
        public void AddFaction(string factionId, int initialReputation = 0)
        {
            if (FactionReputations.ContainsKey(factionId))
                throw new InvalidOperationException("Faction already exists in campaign");
            ApplyChange(new FactionAdded(Id, factionId, initialReputation));
        }

        /// <summary>
        /// Изменяет репутацию фракции на указанную величину (с ограничением -100..100).
        /// </summary>
        /// <param name="factionId">Идентификатор фракции.</param>
        /// <param name="change">Величина изменения (может быть отрицательной).</param>
        /// <exception cref="InvalidOperationException">Если фракция не найдена.</exception>
        public void ChangeFactionReputation(string factionId, int change)
        {
            if (!FactionReputations.ContainsKey(factionId))
                throw new InvalidOperationException("Faction not found");
            ApplyChange(new FactionReputationChanged(Id, factionId, change));
        }

        /// <summary>
        /// Устанавливает значение глобального флага кампании.
        /// </summary>
        /// <param name="flagName">Имя флага.</param>
        /// <param name="value">Значение флага.</param>
        public void SetGlobalFlag(string flagName, string value)
        {
            ApplyChange(new GlobalFlagSet(Id, flagName, value));
        }

        /// <summary>
        /// Удаляет глобальный флаг кампании.
        /// </summary>
        /// <param name="flagName">Имя флага.</param>
        /// <exception cref="InvalidOperationException">Если флаг не существует.</exception>
        public void RemoveGlobalFlag(string flagName)
        {
            if (!GlobalFlags.ContainsKey(flagName))
                throw new InvalidOperationException("Flag not found");
            ApplyChange(new GlobalFlagRemoved(Id, flagName));
        }

        /// <summary>
        /// Продвигает игровое время на указанное количество минут.
        /// </summary>
        /// <param name="minutes">Количество минут (должно быть положительным).</param>
        /// <exception cref="ArgumentException">Если значение минут не положительное.</exception>
        public void AdvanceTime(int minutes)
        {
            if (minutes <= 0)
                throw new ArgumentException("Minutes must be positive");
            ApplyChange(new GameTimeAdvanced(Id, minutes));
        }

        /// <summary>
        /// Изменяет текущую погоду.
        /// </summary>
        /// <param name="newWeather">Новое описание погоды.</param>
        /// <exception cref="ArgumentException">Если строка погоды пустая или содержит только пробелы.</exception>
        public void ChangeWeather(string newWeather)
        {
            if (string.IsNullOrWhiteSpace(newWeather))
                throw new ArgumentException("Weather cannot be empty");
            ApplyChange(new WeatherChanged(Id, newWeather));
        }

        /// <summary>
        /// Открывает новый регион. Если регион уже открыт, событие не создаётся (идемпотентность).
        /// </summary>
        /// <param name="regionName">Название региона.</param>
        public void DiscoverRegion(string regionName)
        {
            if (DiscoveredRegions.Contains(regionName))
                return; // уже открыто, не дублируем событие
            ApplyChange(new RegionDiscovered(Id, regionName));
        }
    }

    /// <summary>
    /// Статус квеста в рамках кампании.
    /// </summary>
    public enum QuestStatus
    {
        /// <summary>Квест доступен для принятия.</summary>
        Available,

        /// <summary>Квест принят и активен.</summary>
        Active,

        /// <summary>Квест завершён.</summary>
        Completed,

        /// <summary>Квест провален.</summary>
        Failed
    }

    /// <summary>
    /// Внутреннее представление квеста в состоянии агрегата кампании.
    /// Содержит идентификатор, название, статус, цели, награды и временные метки.
    /// </summary>
    public class CampaignQuestInfo
    {
        /// <summary>Идентификатор квеста.</summary>
        public Guid QuestId { get; set; }

        /// <summary>Название квеста.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Текущий статус квеста.</summary>
        public QuestStatus Status { get; set; } = QuestStatus.Available;

        /// <summary>Список целей квеста.</summary>
        public List<QuestObjectiveData> Objectives { get; set; } = [];

        /// <summary>Список наград за квест.</summary>
        public List<QuestRewardData> Rewards { get; set; } = [];

        /// <summary>Дата и время выдачи квеста (UTC).</summary>
        public DateTime IssuedAt { get; set; }

        /// <summary>Дата и время завершения квеста (если завершён).</summary>
        public DateTime? CompletedAt { get; set; }
    }
}