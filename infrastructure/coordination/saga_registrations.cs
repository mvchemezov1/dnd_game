// infrastructure/coordination/saga_registrations.cs
using dnd_game.Application.Projections;
using dnd_game.Domain.Events;
using dnd_game.Domain.Interfaces;
using dnd_game.Domain.Sagas;
using dnd_game.infrastructure.message_bus;
using dnd_game.Infrastructure.MessageBus;
using Microsoft.Extensions.DependencyInjection;

namespace dnd_game.Infrastructure.Coordination
{
    /// <summary>
    /// Регистрирует фабрики саг в ISagaRegistry И подписывает ISagaDispatcher на
    /// соответствующие события через IEventBus.
    ///
    /// Раньше в проекте были готовые классы саг (TradeSaga/QuestSaga/LevelUpSaga),
    /// формально реализующие ISaga, но полностью отключённые от реальной обработки событий
    /// по ДВУМ независимым причинам (обе исправлены здесь):
    /// 1. Ни для одного события не было зарегистрировано фабрики через ISagaRegistry.Register —
    ///    SagaCoordinator.DispatchAsync всегда находил пустой список фабрик.
    /// 2. Даже если бы фабрики были зарегистрированы, ничто не вызывало
    ///    ISagaDispatcher.DispatchAsync для реальных событий, публикуемых через IEventBus —
    ///    координатор саг был полностью изолирован от шины событий.
    ///
    /// Регистрация и подписка сделаны вместе (RegisterSaga), чтобы не растащить их
    /// по разным местам кода — где есть фабрика, там же есть и подписка на шину, и наоборот.
    ///
    /// Вызывается один раз при старте приложения, после builder.Build(), когда singleton-
    /// зависимости уже доступны через IServiceProvider (см. Program.cs).
    /// </summary>
    public static class SagaRegistrations
    {
        public static void RegisterAll(IServiceProvider services)
        {
            var registry = services.GetRequiredService<ISagaRegistry>();
            var dispatcher = services.GetRequiredService<ISagaDispatcher>();
            var eventBus = services.GetRequiredService<IEventBus>();
            var commandBus = services.GetRequiredService<ICommandBus>();
            var characterProjection = services.GetRequiredService<CharacterProjection>();
            var campaignProjection = services.GetRequiredService<CampaignProjection>();
            var questTrackingStore = services.GetRequiredService<IQuestTrackingStore>();

            void RegisterSaga<TEvent>(Func<TEvent, ISaga> factory) where TEvent : IDomainEvent
            {
                registry.Register(factory);
                eventBus.Subscribe<TEvent>((e, ct) => dispatcher.DispatchAsync(e, ct));
            }

            // ---- TradeSaga: один инстанс на одну сделку (SagaId = OfferId) ----
            RegisterSaga<TradeOfferCreated>(e => new TradeSaga(e.OfferId, commandBus, eventBus, characterProjection));
            RegisterSaga<TradeOfferAccepted>(e => new TradeSaga(e.OfferId, commandBus, eventBus, characterProjection));
            RegisterSaga<TradeOfferDeclined>(e => new TradeSaga(e.OfferId, commandBus, eventBus, characterProjection));
            RegisterSaga<TradeOfferCancelled>(e => new TradeSaga(e.OfferId, commandBus, eventBus, characterProjection));

            // ---- QuestSaga: один инстанс на один квест (SagaId = QuestId) ----
            RegisterSaga<QuestAccepted>(e => new QuestSaga(e.QuestId, commandBus, campaignProjection, characterProjection, questTrackingStore));
            RegisterSaga<QuestObjectiveUpdated>(e => new QuestSaga(e.QuestId, commandBus, campaignProjection, characterProjection, questTrackingStore));
            RegisterSaga<QuestCompleted>(e => new QuestSaga(e.QuestId, commandBus, campaignProjection, characterProjection, questTrackingStore));
            RegisterSaga<QuestFailed>(e => new QuestSaga(e.QuestId, commandBus, campaignProjection, characterProjection, questTrackingStore));

            // ---- LevelUpSaga: один инстанс на одного персонажа (SagaId = CharacterId) ----
            RegisterSaga<ExperienceGained>(e => new LevelUpSaga(e.CharacterId, commandBus, characterProjection));

            // CharacterDied НЕ несёт QuestId, поэтому не может адресовать конкретный
            // QuestSaga-инстанс напрямую (SagaId = CharacterId, а не QuestId — состояние
            // реального квеста при этом НЕ загружается). Обработчик OnCharacterDied в
            // QuestSaga сам находит затронутые квесты через IQuestTrackingStore и
            // CampaignId для каждого — тоже через IQuestTrackingStore (см. SetCampaign в
            // OnQuestAccepted), а не через _state этого одноразового инстанса.
            RegisterSaga<CharacterDied>(e => new QuestSaga(e.CharacterId, commandBus, campaignProjection, characterProjection, questTrackingStore));

            // ---- CombatSaga: один инстанс на один бой (SagaId = CombatId) ----
            // Эта сага уже была спроектирована под модель "один инстанс на одну сущность" —
            // но содержала баг: SagaId выставлялся только внутри OnCombatStarted как случайный
            // Guid.NewGuid(), из-за чего SagaCoordinator падал на первом же обращении к
            // saga.SagaId (см. историю правок combat_saga.cs). Исправлено вместе с этой
            // регистрацией.
            RegisterSaga<CombatStarted>(e => new CombatSaga(e.CombatId, commandBus));
            RegisterSaga<InitiativeRolled>(e => new CombatSaga(e.CombatId, commandBus));
            RegisterSaga<CombatRoundStarted>(e => new CombatSaga(e.CombatId, commandBus));
            RegisterSaga<CombatTurnEnded>(e => new CombatSaga(e.CombatId, commandBus));
            RegisterSaga<ParticipantRemovedFromCombat>(e => new CombatSaga(e.CombatId, commandBus));
            // CharacterDied для CombatSaga (в отличие от QuestSaga выше) сознательно не
            // регистрируется: CombatSaga не имеет аналогичного tracking-механизма для поиска
            // "в каком бою участвовал этот персонаж", а придумывать его сейчас не стал —
            // это отдельная задача, а не часть текущего исправления.
        }
    }
}
