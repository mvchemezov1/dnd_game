using dnd_game.application.security;
using dnd_game.Application.EventHandlers;
using dnd_game.Application.Projections;
using dnd_game.Application.Security;
using dnd_game.Application.Services;
using dnd_game.Domain.Events;
using dnd_game.infrastructure.event_store;
using dnd_game.Infrastructure.AI;
using dnd_game.Infrastructure.Caching;
using dnd_game.Infrastructure.Common;
using dnd_game.Infrastructure.Coordination;
using dnd_game.Infrastructure.EventStore;
using dnd_game.Infrastructure.Exceptions;
using dnd_game.Infrastructure.MessageBus;
using dnd_game.Infrastructure.Monitoring;
using dnd_game.Infrastructure.Security;
using dnd_game.migrations;
using dnd_game.Presentation.Api;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;
using System.Reflection;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // ============================================================
        // 0. Настройка Serilog
        // ============================================================
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .MinimumLevel.Override("dnd_game.Infrastructure.Coordination.SagaCoordinator", LogEventLevel.Debug)
            .MinimumLevel.Override("dnd_game.Presentation.Api.WebSocketHandler", LogEventLevel.Information)
            .MinimumLevel.Override("dnd_game.Infrastructure.EventStore.PostgresEventStore", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"
            )
            .WriteTo.File(
                path: "logs/dnd_game-.txt",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();

        var builder = WebApplication.CreateBuilder(args);
        builder.Host.UseSerilog();
        Console.WriteLine("МЕТКА 1: builder создан");

        // ============================================================
        // 1. Инфраструктурные сервисы
        // ============================================================
        builder.Services.AddHttpContextAccessor();
        builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
        builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<JwtSettings>>().Value);
        builder.Services.AddSingleton<ICurrentUserService, CurrentUserService>();
        Console.WriteLine("[LOG] Инфраструктурные сервисы (Jwt, CurrentUser) зарегистрированы");

        // ============================================================
        // 2. Метрики
        // ============================================================
        builder.Services.AddSingleton<IMetricsCollector, MetricsCollector>();
        Console.WriteLine("[LOG] MetricsCollector зарегистрирован");

        // ============================================================
        // 3. Кеш-провайдер
        // ============================================================
        builder.Services.AddSingleton<ICacheProvider>(sp =>
        {
            var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                try
                {
                    var redis = ConnectionMultiplexer.Connect(redisConnectionString);
                    Console.WriteLine("[LOG] Redis подключен");
                    return new RedisCacheProvider(redis);
                }
                catch (Exception ex)
                {
                    var logger = sp.GetService<ILogger<ICacheProvider>>();
                    logger?.LogWarning(ex, "Не удалось подключиться к Redis. Используется NoOpCacheProvider.");
                }
            }
            return new NoOpCacheProvider();
        });
        Console.WriteLine("[LOG] CacheProvider зарегистрирован");

        // ============================================================
        // 4. Блокировки, ConsistencyManager, EventStore
        // ============================================================
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        Console.WriteLine($"[LOG] Строка подключения получена (длина: {connectionString.Length})");

        builder.Services.AddSingleton<IDistributedLockManager, InMemoryLockManager>();
        // ConsistencyManager регистрируется через AddGameServices (с Lazy), поэтому здесь не дублируем

        var snapshotConfig = new SnapshotConfiguration { EventCountInterval = 100 };
        builder.Services.AddSingleton<ISnapshotStore>(sp =>
            new SnapshotStore(connectionString, snapshotConfig));
        Console.WriteLine("[LOG] SnapshotStore зарегистрирован");

        builder.Services.AddSingleton<IEventStore>(sp =>
        {
            var snapshotStore = sp.GetRequiredService<ISnapshotStore>();
            var consistencyManager = sp.GetRequiredService<IConsistencyManager>();
            var logger = sp.GetRequiredService<ILogger<PostgresEventStore>>();
            var metrics = sp.GetRequiredService<IMetricsCollector>();
            var eventBus = sp.GetRequiredService<IEventBus>();
            Console.WriteLine("[LOG] Создание PostgresEventStore...");
            return new PostgresEventStore(connectionString, snapshotStore, consistencyManager, logger, metrics, eventBus);
        });
        Console.WriteLine("[LOG] EventStore зарегистрирован");

        // ============================================================
        // 5. Обработчики и сервисы
        // ============================================================
        builder.Services.AddSingleton<ReplayHandler>();
        builder.Services.AddSingleton<TriggerHandler>();
        builder.Services.AddSingleton<WebhookHandler>();
        builder.Services.AddSingleton<CraftingService>();
        builder.Services.AddSingleton<DialogService>();
        builder.Services.AddSingleton<TradeService>();
        builder.Services.AddSingleton<AuthProvider>();
        builder.Services.AddSingleton<PolicyEnforcer>();
        builder.Services.AddSingleton<ScriptEngine>();
        builder.Services.AddSingleton<WebSocketHandler>();
        Console.WriteLine("[LOG] Все обработчики и сервисы зарегистрированы");

        // ============================================================
        // 6. Проекции
        // ============================================================
        builder.Services.AddSingleton<CharacterProjection>(sp =>
        {
            var cache = sp.GetRequiredService<ICacheProvider>();
            return new CharacterProjection(cache, TimeSpan.FromMinutes(5));
        });
        builder.Services.AddSingleton<CombatProjection>(sp =>
        {
            var cache = sp.GetRequiredService<ICacheProvider>();
            return new CombatProjection(cache, TimeSpan.FromMinutes(1));
        });
        builder.Services.AddSingleton<CampaignProjection>(sp =>
        {
            var cache = sp.GetRequiredService<ICacheProvider>();
            return new CampaignProjection(cache, TimeSpan.FromMinutes(10));
        });
        Console.WriteLine("[LOG] Проекции зарегистрированы");

        // ============================================================
        // 7. Миграции (DatabaseMigrator) – регистрируем ДО app.Build()
        // ============================================================
        builder.Services.AddSingleton<DatabaseMigrator>(sp =>
        {
            var connString = builder.Configuration.GetConnectionString("DefaultConnection");
            var logger = sp.GetRequiredService<ILogger<DatabaseMigrator>>();
            return new DatabaseMigrator(connString!, logger);
        });
        Console.WriteLine("[LOG] DatabaseMigrator зарегистрирован");

        // ============================================================
        // 8. Основные сервисы (AddGameServices)
        // ============================================================
        builder.Services.AddGameServices(builder.Configuration);
        Console.WriteLine("[LOG] AddGameServices завершён");

        // ============================================================
        // 9. ASP.NET Core + Swagger + FluentValidation
        // ============================================================
        builder.Services.AddControllers();
        builder.Services.AddFluentValidationAutoValidation();
        builder.Services.AddValidatorsFromAssemblyContaining<Program>();

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "DnD Game API",
                Version = "v1",
                Description = "Backend для D&D-подобной RPG на Event Sourcing + CQRS"
            });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT токен в формате: Bearer {token}",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
                c.IncludeXmlComments(xmlPath);
        });

        Console.WriteLine("[LOG] Контроллеры, ProblemDetails, GlobalExceptionHandler, Swagger и FluentValidation добавлены");

        // ============================================================
        // 10. CORS
        // ============================================================
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (allowedOrigins == null || allowedOrigins.Length == 0)
        {
            if (builder.Environment.IsDevelopment())
            {
                builder.Services.AddCors(options =>
                {
                    options.AddPolicy("AllowSpecificOrigins", policy =>
                    {
                        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                    });
                });
                Console.WriteLine("⚠️ CORS: разрешены все источники (только для разработки)");
            }
            else
            {
                throw new InvalidOperationException("CORS: список разрешённых источников не задан в конфигурации.");
            }
        }
        else
        {
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigins", policy =>
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                });
            });
            Console.WriteLine($"[LOG] CORS настроен для {allowedOrigins.Length} источников");
        }

        // ============================================================
        // 11. Построение приложения
        // ============================================================
        var app = builder.Build();

        // Применяем миграции (DatabaseMigrator уже зарегистрирован в DI)
        var migrator = app.Services.GetRequiredService<DatabaseMigrator>();
        if (!migrator.Migrate())
        {
            Console.WriteLine("Migration failed. Exiting.");
            return;
        }
        Console.WriteLine("МЕТКА 2: app.Build() и миграции прошли");

        // Регистрация саг (после построения контейнера)
        dnd_game.Infrastructure.Coordination.SagaRegistrations.RegisterAll(app.Services);
        Console.WriteLine("МЕТКА 5.1: Саги зарегистрированы");

        // ============================================================
        // ✅ ПОДПИСКА ПРОЕКЦИЙ НА СОБЫТИЯ
        // ============================================================
        var eventBus = app.Services.GetRequiredService<IEventBus>();
        var characterProjection = app.Services.GetRequiredService<CharacterProjection>();
        var combatProjection = app.Services.GetRequiredService<CombatProjection>();
        var campaignProjection = app.Services.GetRequiredService<CampaignProjection>();

        // Character events
        eventBus.Subscribe<CharacterCreated>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<CharacterUpdated>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<CharacterDamageTaken>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<CharacterHealed>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<TemporaryHitPointsSet>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<ExperienceGained>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<CharacterLevelUp>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<AbilityScoreSet>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<SkillProficiencyAdded>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<SkillProficiencyRemoved>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<SavingThrowProficiencyAdded>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<SavingThrowProficiencyRemoved>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<RaceChosen>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<ClassChosen>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<BackgroundChosen>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<FeatAdded>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<FeatRemoved>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<SpellAdded>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<SpellRemoved>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<SpellSlotUsed>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<SpellSlotsRestored>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<ConditionApplied>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<ConditionRemoved>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<AllConditionsCleared>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<ArmorClassUpdated>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<SpeedUpdated>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<ResistanceAdded>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<ResistanceRemoved>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<VulnerabilityAdded>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<VulnerabilityRemoved>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<ImmunityAdded>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<ImmunityRemoved>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<DeathSavingThrowSuccess>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<DeathSavingThrowFailure>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<CharacterStabilized>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<CharacterDied>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<CharacterRevived>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<ItemEquipped>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<ItemUnequipped>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<InventoryItemAdded>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<InventoryItemRemoved>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<HitDieSpent>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<HitDiceRecovered>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<ConcentrationStarted>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<ConcentrationEnded>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<GoldAdded>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<GoldSpent>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<GoldSet>((e, ct) => { characterProjection.Apply(e); return Task.CompletedTask; });

        // Combat events
        eventBus.Subscribe<CombatStarted>((e, ct) => { combatProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<CombatEnded>((e, ct) => { combatProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<InitiativeRolled>((e, ct) => { combatProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<CombatRoundStarted>((e, ct) => { combatProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<CombatTurnStarted>((e, ct) => { combatProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<CombatTurnEnded>((e, ct) => { combatProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<CombatActionTaken>((e, ct) => { combatProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<CombatBonusActionTaken>((e, ct) => { combatProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<CombatReactionUsed>((e, ct) => { combatProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<CombatMovementUsed>((e, ct) => { combatProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<ConditionAppliedToCombatant>((e, ct) => { combatProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<ConditionRemovedFromCombatant>((e, ct) => { combatProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<CombatConcentrationStarted>((e, ct) => { combatProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<CombatConcentrationEnded>((e, ct) => { combatProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<ParticipantAddedToCombat>((e, ct) => { combatProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<ParticipantRemovedFromCombat>((e, ct) => { combatProjection.Apply(e); return Task.CompletedTask; });

        // Campaign events
        eventBus.Subscribe<CampaignCreated>((e, ct) => { campaignProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<QuestCreated>((e, ct) => { campaignProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<QuestAccepted>((e, ct) => { campaignProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<QuestCompleted>((e, ct) => { campaignProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<QuestFailed>((e, ct) => { campaignProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<QuestObjectiveUpdated>((e, ct) => { campaignProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<FactionDiscovered>((e, ct) => { campaignProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<FactionReputationChanged>((e, ct) => { campaignProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<GameTimeAdvanced>((e, ct) => { campaignProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<WeatherChanged>((e, ct) => { campaignProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<RegionDiscovered>((e, ct) => { campaignProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<GlobalFlagSet>((e, ct) => { campaignProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<GlobalFlagRemoved>((e, ct) => { campaignProjection.Apply(e); return Task.CompletedTask; });
        eventBus.Subscribe<WorldEventTriggered>((e, ct) => { campaignProjection.Apply(e); return Task.CompletedTask; });

        Console.WriteLine("[LOG] Проекции подписаны на события");

        // ============================================================
        // 12. Middleware
        // ============================================================
        app.UseCors("AllowSpecificOrigins");
        app.UseStaticFiles();

        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "DnD Game API v1");
            c.RoutePrefix = "swagger";
        });

        app.UseWebSockets();

        app.Map("/ws", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var handler = context.RequestServices.GetRequiredService<WebSocketHandler>();
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await handler.HandleAsync(
                webSocket,
                context,
                context.RequestAborted,
                context.Connection.RemoteIpAddress
            );
        });
        Console.WriteLine("[LOG] Маршрут /ws настроен");

        app.MapControllers();
        app.MapFallbackToFile("index.html");
        Console.WriteLine("[LOG] Контроллеры и fallback-маршруты добавлены");

        var url = Environment.GetEnvironmentVariable("APP_URL") ?? "http://0.0.0.0:5000";
        Console.WriteLine($"МЕТКА 6: перед RunAsync, url={url}");

        try
        {
            Console.WriteLine($"[LOG] Запуск приложения на {url} ...");
            await app.RunAsync(url);
            Console.WriteLine("[LOG] app.RunAsync завершился штатно");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ОШИБКА] При выполнении app.RunAsync: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            throw;
        }
        finally
        {
            Console.WriteLine($"[END] Выход из Main, время: {DateTime.Now:HH:mm:ss.fff}");
        }
    }
}