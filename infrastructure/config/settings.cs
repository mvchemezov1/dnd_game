// infrastructure/config/settings.cs
namespace dnd_game.Infrastructure.Config
{
    /// <summary>
    /// Корневой объект конфигурации приложения DnD.
    /// Содержит все настройки, необходимые для функционирования игры.
    /// </summary>
    public class Settings
    {
        // ---------- Подключения ----------
        public string DbConnectionString { get; set; } = "";
        public string RabbitMqHost { get; set; } = "localhost";
        public int RabbitMqPort { get; set; } = 5672;
        public string RedisConnectionString { get; set; } = ""; // для кэширования / saga state

        // ---------- Хранилище событий ----------
        public EventStoreSettings EventStore { get; set; } = new();

        // ---------- Игровые правила (значения по умолчанию) ----------
        public GameRulesSettings GameRules { get; set; } = new();

        // ---------- Поведение AI ----------
        public AiSettings Ai { get; set; } = new();

        // ---------- Безопасность и аутентификация ----------
        public SecuritySettings Security { get; set; } = new();

        // ---------- Уведомления и вебхуки ----------
        public NotificationSettings Notifications { get; set; } = new();

        // ---------- Логирование и аудит ----------
        public LoggingSettings Logging { get; set; } = new();

        // ---------- UI / Отображение ----------
        public UiSettings Ui { get; set; } = new();

        // ---------- Технические лимиты ----------
        public TechnicalLimits Limits { get; set; } = new();
    }

    /// <summary>
    /// Настройки Event Store.
    /// </summary>
    public class EventStoreSettings
    {
        public string Provider { get; set; } = "Postgres";   // Postgres, EventStoreDB, InMemory
        public bool EnableSnapshotting { get; set; } = true;
        public int SnapshotInterval { get; set; } = 100;      // каждые N событий
        public int MaxReadPageSize { get; set; } = 500;
    }

    /// <summary>
    /// Настройки игровых правил по умолчанию (могут переопределяться кампанией).
    /// </summary>
    public class GameRulesSettings
    {
        public int StandardPointBuyPoints { get; set; } = 27;
        public int MaxLevel { get; set; } = 20;
        public bool EncumberanceEnabled { get; set; } = false;
        public bool FlankingAdvantage { get; set; } = false;
        public bool DiagonalMovementCostExtra { get; set; } = false; // опциональное правило 3.5/Pathfinder
        public string InitiativeResolution { get; set; } = "DexterityCheck"; // или "GroupInitiative", "D20DexMod"
        public bool MilestoneLeveling { get; set; } = false;
        public bool SpellPointsVariant { get; set; } = false;
        public int PassivePerceptionBase { get; set; } = 10;

        // Таблица опыта (если не milestone)
        public Dictionary<int, int> ExperienceThresholds { get; set; } = new()
        {
            {2, 300}, {3, 900}, {4, 2700}, {5, 6500}, {6, 14000},
            {7, 23000}, {8, 34000}, {9, 48000}, {10, 64000},
            {11, 85000}, {12, 100000}, {13, 120000}, {14, 140000},
            {15, 165000}, {16, 195000}, {17, 225000}, {18, 265000},
            {19, 305000}, {20, 355000}
        };
    }

    /// <summary>
    /// Настройки искусственного интеллекта.
    /// </summary>
    public class AiSettings
    {
        public bool EnableMonsterAi { get; set; } = true;
        public bool EnableNpcBehaviorTrees { get; set; } = true;
        public int AiTickIntervalMs { get; set; } = 500;     // как часто AI пересматривает состояние
        public int PerceptionRefreshIntervalMs { get; set; } = 2000;

        // Пороги здоровья для смены поведения
        public float LowHealthThreshold { get; set; } = 0.25f;
        public float CriticalHealthThreshold { get; set; } = 0.10f;

        // Настройки blackboard
        public int BlackboardDefaultFactExpirationSeconds { get; set; } = 60;
        public int BlackboardMemoryRetentionMinutes { get; set; } = 30;
    }

    /// <summary>
    /// Настройки безопасности.
    /// </summary>
    public class SecuritySettings
    {
        public string JwtSecret { get; set; } = "change-me-in-production";
        public int JwtExpirationMinutes { get; set; } = 1440;
        public bool EnableHmacWebhookSigning { get; set; } = true;
        public int WebhookSignatureToleranceSeconds { get; set; } = 300;

        // Настройки CORS
        public string[] AllowedOrigins { get; set; } = { "http://localhost:3000" };
    }

    /// <summary>
    /// Настройки уведомлений и интеграций.
    /// </summary>
    public class NotificationSettings
    {
        public bool EnableInGameNotifications { get; set; } = true;
        public bool EnablePushNotifications { get; set; } = false;
        public string PushApiKey { get; set; } = "";
        public string EmailSmtpHost { get; set; } = "";
        public int EmailSmtpPort { get; set; } = 587;

        // Webhook defaults
        public int WebhookMaxRetries { get; set; } = 3;
        public int WebhookTimeoutSeconds { get; set; } = 10;
    }

    /// <summary>
    /// Настройки логирования.
    /// </summary>
    public class LoggingSettings
    {
        public bool EnableCombatLog { get; set; } = true;
        public bool EnableDetailedMetrics { get; set; } = true;
        public bool LogAllDomainEvents { get; set; } = false; // для отладки
        public string MetricsExporter { get; set; } = "Prometheus";
    }

    /// <summary>
    /// Настройки пользовательского интерфейса.
    /// </summary>
    public class UiSettings
    {
        public bool ShowDiceRolls { get; set; } = true;
        public bool DarkModeByDefault { get; set; } = true;
        public int AutoSaveCharacterIntervalMinutes { get; set; } = 5;
    }

    /// <summary>
    /// Технические ограничения (защита от злоупотреблений).
    /// </summary>
    public class TechnicalLimits
    {
        public int MaxCharacterNameLength { get; set; } = 50;
        public int MaxInventoryItems { get; set; } = 500;
        public int MaxSpellsKnown { get; set; } = 300;
        public int MaxParticipantsPerCombat { get; set; } = 50;
        public int MaxActiveConditionsPerCharacter { get; set; } = 20;
        public int MaxTradeItemsPerOffer { get; set; } = 20;
    }
}