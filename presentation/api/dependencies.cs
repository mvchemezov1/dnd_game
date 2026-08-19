// presentation/api/dependencies.cs
using dnd_game.application.command_handlers;
using dnd_game.application.event_handlers;
using dnd_game.application.query_handlers;
using dnd_game.Application.CommandHandlers;
using dnd_game.Application.EventHandlers;
using dnd_game.Application.Projections;
using dnd_game.Application.Security;
using dnd_game.Application.Services;
using dnd_game.Domain.Commands;
using dnd_game.Domain.Interfaces;
using dnd_game.Domain.Queries;
using dnd_game.Domain.Sagas;
using dnd_game.infrastructure.event_store;
using dnd_game.infrastructure.message_bus;
using dnd_game.Infrastructure.AI;
using dnd_game.Infrastructure.Caching;
using dnd_game.Infrastructure.Common;
using dnd_game.Infrastructure.Config;
using dnd_game.Infrastructure.Coordination;
using dnd_game.Infrastructure.EventStore;
using dnd_game.Infrastructure.Localization;
using dnd_game.Infrastructure.MessageBus;
using dnd_game.Infrastructure.Monitoring;
using dnd_game.Infrastructure.Network;
using dnd_game.Infrastructure.Security;
using dnd_game.Infrastructure.Undo;
using dnd_game.Infrastructure.World;
using dnd_game.migrations;
using dnd_game.Presentation.Api.Validators;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace dnd_game.Presentation.Api;

public static class Dependencies
{
    public static IServiceCollection AddGameServices(this IServiceCollection services, IConfiguration configuration)
    {
        // =====================================================================
        // Конфигурация
        // =====================================================================
        services.Configure<Settings>(configuration.GetSection("Game"));
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.Configure<TokenSettings>(configuration.GetSection("Token"));

        var tokenSecret = configuration["Token:Secret"];
        if (string.IsNullOrWhiteSpace(tokenSecret)
            || tokenSecret is "change-me" or "your-secret-key" or "your-secret-key-change-in-production")
        {
            throw new InvalidOperationException(
                "Token:Secret is not configured (or still has its placeholder value). Set it via the " +
                "Token__Secret environment variable, or locally via " +
                "'dotnet user-secrets set \"Token:Secret\" \"<a long random value>\"'.");
        }

        services.Configure<RateLimitConfiguration>(configuration.GetSection("RateLimiting"));
        services.AddSingleton<IDistributedLockManager, InMemoryLockManager>();
        services.AddSingleton<IReplayEventStore, InMemoryReplayEventStore>();
        services.AddSingleton<ICurrentSessionProvider, DefaultCurrentSessionProvider>();
        services.AddSingleton<INarrativeLogBuilder, DefaultNarrativeLogBuilder>();
        services.AddSingleton<ITriggerDefinitionRepository, InMemoryTriggerDefinitionRepository>();
        services.AddSingleton<IWebhookSubscriptionRepository, InMemoryWebhookSubscriptionRepository>();
        services.AddSingleton<IRecipeRepository, InMemoryRecipeRepository>();
        services.AddSingleton<IDialogueRepository, InMemoryDialogueRepository>();
        services.AddSingleton<IUserSecurityContextProvider, dnd_game.Infrastructure.Security.HttpUserSecurityContextProvider>();
        services.AddSingleton<IScriptRepository, InMemoryScriptRepository>();
        services.AddSingleton<ISessionManager, SessionManager>();
        services.AddSingleton<IRateLimiter, RateLimiter>();
        services.AddSingleton<IQuestTrackingStore, InMemoryQuestTrackingStore>();
        services.AddSingleton<InMemoryBus>();
        services.AddScoped<CraftingController>();
        services.AddScoped<TradeController>();
        services.AddScoped<DialogController>();
        services.AddScoped<TravelController>();
        services.AddSingleton<UndoManager>();

        // Шина команд/событий – всегда InMemory (RabbitMQ отключён)
        services.AddSingleton<ICommandBus>(sp => sp.GetRequiredService<InMemoryBus>());
        services.AddSingleton<IQueryBus>(sp => sp.GetRequiredService<InMemoryBus>());
        services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<InMemoryBus>());

        // =====================================================================
        // Хранилище событий
        // =====================================================================
        var connString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connString))
            throw new InvalidOperationException("Connection string 'DefaultConnection' is null or empty.");

        services.AddSingleton<IUserRepository>(sp => new PostgresUserRepository(connString));
        services.AddSingleton<IRefreshTokenStore>(sp => new PostgresRefreshTokenStore(connString));
        services.AddSingleton<ISnapshotStore>(sp =>
        {
            var config = new SnapshotConfiguration { EventCountInterval = 100 };
            return new SnapshotStore(connString, config);
        });

        // ConsistencyManager – передаём IServiceProvider для ленивого разрешения IEventStore
        services.AddSingleton<IConsistencyManager>(sp =>
        {
            var lockManager = sp.GetRequiredService<IDistributedLockManager>();
            var logger = sp.GetRequiredService<ILogger<ConsistencyManager>>();
            var metrics = sp.GetRequiredService<IMetricsCollector>();
            return new ConsistencyManager(sp, lockManager, logger, metrics);
        });

        services.AddSingleton<IEventStore>(sp =>
        {
            var snapshotStore = sp.GetRequiredService<ISnapshotStore>();
            var consistencyManager = sp.GetRequiredService<IConsistencyManager>();
            var logger = sp.GetRequiredService<ILogger<PostgresEventStore>>();
            var metrics = sp.GetRequiredService<IMetricsCollector>();
            return new PostgresEventStore(connString, snapshotStore, consistencyManager, logger, metrics);
        });

        // =====================================================================
        // Проекции (read‑model)
        // =====================================================================
        services.AddSingleton<CharacterProjection>();
        services.AddSingleton<CombatProjection>();
        services.AddSingleton<CampaignProjection>();

        // =====================================================================
        // Обработчики запросов (Query Handlers)
        // =====================================================================
        services.AddSingleton<IQueryHandler<GetCharacterById, CharacterDto?>, CharacterQueryHandler>();
        services.AddSingleton<IQueryHandler<GetAllCharacters, List<CharacterDto>>, CharacterQueryHandler>();
        services.AddSingleton<IQueryHandler<GetCharacterHitPoints, CharacterHitPointsDto?>, CharacterQueryHandler>();
        services.AddSingleton<IQueryHandler<GetCharacterCombatStats, CharacterCombatStatsDto?>, CharacterQueryHandler>();
        services.AddSingleton<IQueryHandler<GetCharacterSpells, CharacterSpellsDto?>, CharacterQueryHandler>();
        services.AddSingleton<IQueryHandler<GetCharacterInventory, List<InventoryItemDto>>, CharacterQueryHandler>();
        services.AddSingleton<IQueryHandler<GetCharacterEquipment, List<EquippedItemDto>>, CharacterQueryHandler>();
        services.AddSingleton<IQueryHandler<GetCharacterDeathStatus, CharacterDeathStatusDto?>, CharacterQueryHandler>();
        services.AddSingleton<IQueryHandler<GetCharacterConditions, List<string>>, CharacterQueryHandler>();
        services.AddSingleton<IQueryHandler<GetCharacterDefenses, CharacterDefensesDto?>, CharacterQueryHandler>();
        services.AddSingleton<IQueryHandler<SearchCharacters, List<CharacterSummaryDto>>, CharacterQueryHandler>();

        services.AddSingleton<IQueryHandler<GetCombatStatus, CombatStatusDto?>, CombatQueryHandler>();
        services.AddSingleton<IQueryHandler<GetCombatParticipants, List<CombatParticipantDto>>, CombatQueryHandler>();
        services.AddSingleton<IQueryHandler<GetCurrentCombatParticipant, CombatParticipantDto?>, CombatQueryHandler>();
        services.AddSingleton<IQueryHandler<GetCombatRound, int>, CombatQueryHandler>();
        services.AddSingleton<IQueryHandler<GetCombatTurnOrder, List<Guid>>, CombatQueryHandler>();
        services.AddSingleton<IQueryHandler<IsCombatActive, bool>, CombatQueryHandler>();

        services.AddSingleton<IQueryHandler<GetActiveQuests, List<Guid>>, CampaignQueryHandler>();
        services.AddSingleton<IQueryHandler<GetQuestDetails, QuestInfo?>, CampaignQueryHandler>();
        services.AddSingleton<IQueryHandler<GetQuestsByStatus, List<QuestInfo>>, CampaignQueryHandler>();
        services.AddSingleton<IQueryHandler<GetCampaignState, CampaignState?>, CampaignQueryHandler>();
        services.AddSingleton<IQueryHandler<GetFactionReputation, FactionState?>, CampaignQueryHandler>();
        services.AddSingleton<IQueryHandler<GetAllFactions, List<FactionState>>, CampaignQueryHandler>();
        services.AddSingleton<IQueryHandler<GetActiveWorldEvents, List<string>>, CampaignQueryHandler>();

        // Заглушки репозиториев
        services.AddSingleton<ICharacterOwnershipRepository, CharacterOwnershipRepository>();
        services.AddSingleton<IReplayEventStore, InMemoryReplayEventStore>();
        services.AddSingleton<ITriggerDefinitionRepository, InMemoryTriggerDefinitionRepository>();
        services.AddSingleton<IWebhookSubscriptionRepository, InMemoryWebhookSubscriptionRepository>();
        services.AddSingleton<ISagaStateRepository, InMemorySagaStateRepository>();
        services.AddSingleton<IRecipeRepository, InMemoryRecipeRepository>();
        services.AddSingleton<ICraftingProcessRepository, InMemoryCraftingProcessRepository>();
        services.AddSingleton<IDialogueRepository, InMemoryDialogueRepository>();
        services.AddSingleton<IScriptRepository, InMemoryScriptRepository>();
        services.AddSingleton<IConditionEvaluator, DefaultConditionEvaluator>();
        services.AddSingleton<ITradeRepository, InMemoryTradeRepository>();
        services.AddSingleton<ITradeOfferRepository, InMemoryTradeOfferRepository>();

        // =====================================================================
        // Обработчики команд (Command Handlers)
        // =====================================================================

        // ---- Character commands ----
        // Существующие
        services.AddSingleton<ICommandHandler<UpdateProficiencyBonus>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<ChooseRace>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<ChooseClass>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<ChooseBackground>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<AddSkillProficiency>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<RemoveSkillProficiency>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<AddSavingThrowProficiency>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<RemoveSavingThrowProficiency>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<AddFeat>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<RemoveFeat>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<PrepareSpell>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<UnprepareSpell>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<UseClassFeature>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<RechargeFeature>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<AttuneItem>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<UnattuneItem>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<AddResistance>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<RemoveResistance>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<AddVulnerability>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<RemoveVulnerability>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<AddImmunity>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<RemoveImmunity>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<ReviveCharacter>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<ResetDeathSavingThrows>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<ClearAllConditionsCommand>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<AddGold>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<SpendGold>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<SetGoldCommand>, CharacterHandler>();

        // Недостающие – добавляем все, что используются в CharactersController
        services.AddSingleton<ICommandHandler<CreateCharacter>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<UpdateCharacter>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<DealDamage>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<HealCharacter>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<SetTemporaryHitPoints>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<SetAbilityScore>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<AddSpell>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<RemoveSpell>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<UseSpellSlot>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<RestoreAllSpellSlots>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<AddInventoryItem>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<RemoveInventoryItem>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<EquipItem>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<UnequipItem>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<ApplyCondition>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<RemoveCondition>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<DeathSavingThrow>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<StabilizeCharacter>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<GainExperience>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<LevelUpCharacter>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<UpdateArmorClass>, CharacterHandler>();
        services.AddSingleton<ICommandHandler<UpdateSpeed>, CharacterHandler>();

        // ---- Movement commands (MoveCharacter обрабатывается MovementHandler) ----
        services.AddSingleton<ICommandHandler<MoveCharacter>, MovementHandler>();
        services.AddSingleton<ICommandHandler<MoveCharacterToPosition>, MovementHandler>();
        services.AddSingleton<ICommandHandler<MoveCharacterWithDash>, MovementHandler>();
        services.AddSingleton<ICommandHandler<MoveCharacterWithDisengage>, MovementHandler>();
        services.AddSingleton<ICommandHandler<MoveCharacterStealthily>, MovementHandler>();
        services.AddSingleton<ICommandHandler<ClimbCharacter>, MovementHandler>();
        services.AddSingleton<ICommandHandler<SwimCharacter>, MovementHandler>();
        services.AddSingleton<ICommandHandler<FlyCharacter>, MovementHandler>();
        services.AddSingleton<ICommandHandler<BurrowCharacter>, MovementHandler>();
        services.AddSingleton<ICommandHandler<JumpCharacter>, MovementHandler>();
        services.AddSingleton<ICommandHandler<SetCharacterSpeed>, MovementHandler>();
        services.AddSingleton<ICommandHandler<ResetCharacterSpeed>, MovementHandler>();
        services.AddSingleton<ICommandHandler<ApplyDifficultTerrain>, MovementHandler>();
        services.AddSingleton<ICommandHandler<RemoveDifficultTerrain>, MovementHandler>();
        services.AddSingleton<ICommandHandler<ApplyMovementImpairment>, MovementHandler>();
        services.AddSingleton<ICommandHandler<RemoveMovementImpairment>, MovementHandler>();
        services.AddSingleton<ICommandHandler<MakeAthleticsCheckForMovement>, MovementHandler>();
        services.AddSingleton<ICommandHandler<MakeAcrobaticsCheckForMovement>, MovementHandler>();
        services.AddSingleton<ICommandHandler<TakeFallDamage>, MovementHandler>();

        // ---- Rest commands ----
        services.AddSingleton<ICommandHandler<StartRest>, RestHandler>();
        services.AddSingleton<ICommandHandler<EndRest>, RestHandler>();
        services.AddSingleton<ICommandHandler<SpendHitDie>, RestHandler>();
        services.AddSingleton<ICommandHandler<InterruptRest>, RestHandler>();

        // ---- Campaign commands ----
        services.AddSingleton<ICommandHandler<AcceptQuestCommand>, CampaignHandler>();
        services.AddSingleton<ICommandHandler<CompleteQuestCommand>, CampaignHandler>();
        services.AddSingleton<ICommandHandler<FailQuestCommand>, CampaignHandler>();
        services.AddSingleton<ICommandHandler<CreateQuestCommand>, CampaignHandler>();
        services.AddSingleton<ICommandHandler<UpdateQuestObjectiveCommand>, CampaignHandler>();

        // ---- Combat commands ----
        services.AddSingleton<ICommandHandler<StartCombat>, CombatHandler>();
        services.AddSingleton<ICommandHandler<EndCombat>, CombatHandler>();
        services.AddSingleton<ICommandHandler<RollInitiative>, CombatHandler>();
        services.AddSingleton<ICommandHandler<StartRound>, CombatHandler>();
        services.AddSingleton<ICommandHandler<NextTurn>, CombatHandler>();
        services.AddSingleton<ICommandHandler<EndRound>, CombatHandler>();
        services.AddSingleton<ICommandHandler<AddParticipantToCombat>, CombatHandler>();
        services.AddSingleton<ICommandHandler<RemoveParticipantFromCombat>, CombatHandler>();
        services.AddSingleton<ICommandHandler<TakeMoveAction>, CombatHandler>();
        services.AddSingleton<ICommandHandler<TakeStandardAction>, CombatHandler>();
        services.AddSingleton<ICommandHandler<TakeBonusAction>, CombatHandler>();
        services.AddSingleton<ICommandHandler<TakeReaction>, CombatHandler>();
        services.AddSingleton<ICommandHandler<ReadyAction>, CombatHandler>();
        services.AddSingleton<ICommandHandler<TriggerReadyAction>, CombatHandler>();
        services.AddSingleton<ICommandHandler<DealDamageToTarget>, CombatHandler>();
        services.AddSingleton<ICommandHandler<HealTarget>, CombatHandler>();
        services.AddSingleton<ICommandHandler<ApplyConditionToTarget>, CombatHandler>();
        services.AddSingleton<ICommandHandler<RemoveConditionFromTarget>, CombatHandler>();
        services.AddSingleton<ICommandHandler<MakeSavingThrowInCombat>, CombatHandler>();
        services.AddSingleton<ICommandHandler<MakeDeathSavingThrowInCombat>, CombatHandler>();
        services.AddSingleton<ICommandHandler<StabilizeInCombat>, CombatHandler>();
        services.AddSingleton<ICommandHandler<MakeConcentrationCheck>, CombatHandler>();
        services.AddSingleton<ICommandHandler<DelayTurn>, CombatHandler>();
        services.AddSingleton<ICommandHandler<SurrenderInCombat>, CombatHandler>();
        services.AddSingleton<ICommandHandler<PerformAction>, CombatHandler>();

        // =====================================================================
        // Обработчики событий
        // =====================================================================
        services.AddSingleton<LoggingHandler>();
        services.AddSingleton<MetricHandler>();
        services.AddSingleton<NotificationHandler>();
        services.AddSingleton<ReplayHandler>();
        services.AddSingleton<TriggerHandler>();
        services.AddSingleton<WebhookHandler>();
        services.AddSingleton<AiHandler>();

        // =====================================================================
        // Саги
        // =====================================================================
        services.AddSingleton<ISagaRegistry, SagaRegistry>();
        services.AddSingleton<SagaCoordinator>();
        services.AddSingleton<ISagaDispatcher>(sp => sp.GetRequiredService<SagaCoordinator>());

        // =====================================================================
        // Сервисы приложения
        // =====================================================================
        services.AddSingleton<CombatService>();
        services.AddSingleton<CraftingService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<TradeService>();
        services.AddSingleton<TravelService>();

        // =====================================================================
        // Безопасность
        // =====================================================================
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IAuthProvider, AuthProvider>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<PermissionChecker>();
        services.AddSingleton<PolicyEnforcer>();

        // =====================================================================
        // AI и восприятие
        // =====================================================================
        services.AddSingleton<IBlackboardStore, BlackboardStore>();
        services.AddSingleton<MonsterAi>();
        services.AddSingleton<PerceptionPipeline>();
        services.AddSingleton<ScriptEngine>();

        // =====================================================================
        // Undo / Redo
        // =====================================================================
        services.AddSingleton<UndoManager>();

        // =====================================================================
        // Локализация
        // =====================================================================
        services.AddSingleton<ILocaleProvider, JsonFileLocaleProvider>(sp =>
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Locales");
            return new JsonFileLocaleProvider(path);
        });
        services.AddSingleton<ILocaleManager, LocaleManager>();

        // =====================================================================
        // Мониторинг (HealthCheck – с учётом возможного отсутствия RabbitMQ)
        // =====================================================================
        services.AddSingleton<IMetricsCollector, MetricsCollector>();
        services.AddSingleton<ITracer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SimpleTracer>>();
            return new SimpleTracer(logger);
        });
        services.AddSingleton<IHealthCheck>(sp =>
        {
            var rabbitMqBus = sp.GetService<RabbitMqBus>(); // может быть null
            return new DndHealthCheck(
                sp.GetRequiredService<IEventStore>(),
                rabbitMqBus,
                sp.GetService<IDistributedLockManager>(),
                sp.GetRequiredService<IOptions<HealthCheckOptions>>(),
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<ILogger<DndHealthCheck>>()
            );
        });

        // =====================================================================
        // Кеширование (Redis / NoOp)
        // =====================================================================
        services.AddSingleton<ICacheProvider>(sp =>
        {
            var redisConnectionString = configuration.GetConnectionString("Redis");
            if (string.IsNullOrEmpty(redisConnectionString))
            {
                var logger = sp.GetService<ILogger<ICacheProvider>>();
                logger?.LogWarning("Redis connection string not configured. Using NoOpCacheProvider.");
                return new NoOpCacheProvider();
            }

            try
            {
                var redis = ConnectionMultiplexer.Connect(redisConnectionString);
                var logger = sp.GetService<ILogger<ICacheProvider>>();
                logger?.LogInformation("Redis cache provider initialized.");
                return new RedisCacheProvider(redis);
            }
            catch (Exception ex)
            {
                var logger = sp.GetService<ILogger<ICacheProvider>>();
                logger?.LogWarning(ex, "Failed to connect to Redis. Using NoOpCacheProvider.");
                return new NoOpCacheProvider();
            }
        });

        services.Configure<RateLimitConfiguration>(configuration.GetSection("RateLimiting"));

        // =====================================================================
        // Сетевые компоненты
        // =====================================================================
        services.AddSingleton<ISessionManager, SessionManager>();
        services.AddSingleton<INetworkProtocol, JsonNetworkProtocol>();
        services.AddSingleton<IRateLimiter, RateLimiter>();
        services.AddSingleton<GameServer>(sp =>
        {
            var config = new GameServerConfiguration();
            return new GameServer(
                config,
                sp,
                sp.GetRequiredService<ICommandBus>(),
                sp.GetRequiredService<IEventBus>(),
                sp.GetRequiredService<ISessionManager>(),
                sp.GetRequiredService<PermissionChecker>(),
                sp.GetRequiredService<IMetricsCollector>(),
                sp.GetRequiredService<ITracer>(),
                sp.GetRequiredService<ILogger<GameServer>>()
            );
        });

        // =====================================================================
        // Мировые сервисы (сетка, видимость)
        // =====================================================================
        services.AddSingleton<Infrastructure.World.IGridProvider>(sp => new GridProvider(100, 100));
        services.AddSingleton<VisibilityCalculator>();

        // =====================================================================
        // HTTP-клиент для вебхуков
        // =====================================================================
        services.AddHttpClient<IWebhookClient, DefaultWebhookClient>();

        // =====================================================================
        // Миграции (DatabaseMigrator)
        // =====================================================================
        services.AddSingleton<DatabaseMigrator>(sp =>
        {
            var connString = configuration.GetConnectionString("DefaultConnection");
            var logger = sp.GetRequiredService<ILogger<DatabaseMigrator>>();
            return new DatabaseMigrator(connString!, logger);
        });

        // =====================================================================
        // Фоновый сервис очистки refresh-токенов
        // =====================================================================
        services.AddHostedService<RefreshTokenCleanupService>();

        // =====================================================================
        // FluentValidation – регистрация валидаторов
        // =====================================================================
        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssemblyContaining<CreateCharacterRequestValidator>();

        // =====================================================================
        // Настройка обработки ошибок валидации ModelState
        // =====================================================================
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value?.Errors.Select(e => e.ErrorMessage).ToArray()
                    );

                return new BadRequestObjectResult(new { errors });
            };
        });

        return services;
    }
}