// domain/sagas/trade_saga.cs
using dnd_game.Domain.Events;
using dnd_game.Domain.Commands;
using dnd_game.Application.Projections; // CharacterProjection
using dnd_game.Infrastructure.MessageBus;
using dnd_game.infrastructure.message_bus; // ICommandBus, IEventBus

namespace dnd_game.Domain.Sagas
{
    /// <summary>
    /// Сага, управляющая полным циклом одной торговой сделки между двумя участниками
    /// (игроками или NPC). Один инстанс TradeSaga = одна сделка: SagaId = OfferId, что
    /// позволяет SagaCoordinator корректно находить и восстанавливать состояние конкретной
    /// сделки через ISagaStateRepository (в т.ч. после сбоя процесса — см.
    /// tests/unit/SagaCoordinatorRecoveryTests.cs на предмет сценария восстановления).
    ///
    /// Регистрация фабрики — см. infrastructure/coordination/saga_registrations.cs:
    /// TradeOfferCreated/Accepted/Declined/Cancelled все несут OfferId, поэтому каждое из
    /// этих событий однозначно адресует один и тот же SagaId.
    /// </summary>
    public class TradeSaga : ISaga, ICompensatingSaga
    {
        private readonly ICommandBus _commandBus;
        private readonly IEventBus _eventBus;
        private readonly CharacterProjection _characterProjection;
        private TradeSagaState _state;

        public TradeSaga(Guid offerId, ICommandBus commandBus, IEventBus eventBus, CharacterProjection characterProjection)
        {
            _commandBus = commandBus;
            _eventBus = eventBus;
            _characterProjection = characterProjection;
            _state = new TradeSagaState { SagaId = offerId, CorrelationId = offerId, OfferId = offerId };
        }

        public Guid SagaId => _state.SagaId;
        public ISagaState State => _state;

        public void LoadState(ISagaState state) => _state = (TradeSagaState)state;

        public async Task Handle(IDomainEvent @event, CancellationToken cancellationToken = default)
        {
            switch (@event)
            {
                case TradeOfferCreated offerCreated:
                    OnTradeOfferCreated(offerCreated);
                    break;
                case TradeOfferAccepted offerAccepted:
                    await OnTradeOfferAccepted(offerAccepted, cancellationToken);
                    break;
                case TradeOfferDeclined offerDeclined:
                    OnTradeOfferDeclined(offerDeclined);
                    break;
                case TradeOfferCancelled offerCancelled:
                    OnTradeOfferCancelled(offerCancelled);
                    break;
                default:
                    break;
            }
        }

        public Task Complete(bool success, string? reason = null, CancellationToken cancellationToken = default)
        {
            _state.Status = success ? SagaStatus.Completed : SagaStatus.Failed;
            if (!success) _state.FailureReason = reason;
            return Task.CompletedTask;
        }

        public async Task Compensate(CancellationToken cancellationToken = default)
        {
            await CompensateTrade(_state);
            _state.Status = SagaStatus.Compensated;
        }

        // ---------- Обработчики событий ----------

        private void OnTradeOfferCreated(TradeOfferCreated e)
        {
            _state.FromCharacterId = e.FromCharacterId;
            _state.ToCharacterId = e.ToCharacterId;
            _state.OfferedItems = e.OfferedItems;
            _state.OfferedGold = e.OfferedGold;
            _state.RequestedItems = e.RequestedItems;
            _state.RequestedGold = e.RequestedGold;
            _state.TradeStatus = TradeSagaStatus.Pending;
            _state.Status = SagaStatus.Started;
            _state.CreatedAt = e.OccurredOn;
        }

        private async Task OnTradeOfferAccepted(TradeOfferAccepted e, CancellationToken cancellationToken)
        {
            if (_state.TradeStatus != TradeSagaStatus.Pending)
                return;

            _state.TradeStatus = TradeSagaStatus.InProgress;
            _state.Status = SagaStatus.InProgress;

            try
            {
                var toChar = await _characterProjection.GetById(_state.ToCharacterId) ?? throw new InvalidOperationException("Receiving character not found.");
                foreach (var item in _state.RequestedItems)
                {
                    var invItem = toChar.Inventory.FirstOrDefault(i => i.ItemId == item.ItemId);
                    if (invItem == null || invItem.Quantity < item.Quantity)
                        throw new InvalidOperationException($"Not enough '{item.ItemName}' to complete trade.");
                }
                if (toChar.Gold < _state.RequestedGold)
                    throw new InvalidOperationException("Not enough gold to complete trade.");

                // Шаг 1: списываем предложенные предметы и золото у инициатора (From)
                foreach (var item in _state.OfferedItems)
                    await _commandBus.SendAsync(new RemoveInventoryItem(_state.FromCharacterId, item.ItemId, item.Quantity), new CommandContext { CancellationToken = cancellationToken });
                if (_state.OfferedGold > 0)
                    await _commandBus.SendAsync(new SpendGold(_state.FromCharacterId, _state.OfferedGold), new CommandContext { CancellationToken = cancellationToken });

                // Шаг 2: списываем запрошенные предметы и золото у получателя (To)
                foreach (var item in _state.RequestedItems)
                    await _commandBus.SendAsync(new RemoveInventoryItem(_state.ToCharacterId, item.ItemId, item.Quantity), new CommandContext { CancellationToken = cancellationToken });
                if (_state.RequestedGold > 0)
                    await _commandBus.SendAsync(new SpendGold(_state.ToCharacterId, _state.RequestedGold), new CommandContext { CancellationToken = cancellationToken });

                // Шаг 3: начисляем предложенные предметы и золото получателю
                foreach (var item in _state.OfferedItems)
                    await _commandBus.SendAsync(new AddInventoryItem(_state.ToCharacterId, item.ItemId, item.ItemName, item.Quantity), new CommandContext { CancellationToken = cancellationToken });
                if (_state.OfferedGold > 0)
                    await _commandBus.SendAsync(new AddGold(_state.ToCharacterId, _state.OfferedGold), new CommandContext { CancellationToken = cancellationToken });

                // Шаг 4: начисляем запрошенные предметы и золото инициатору
                foreach (var item in _state.RequestedItems)
                    await _commandBus.SendAsync(new AddInventoryItem(_state.FromCharacterId, item.ItemId, item.ItemName, item.Quantity), new CommandContext { CancellationToken = cancellationToken });
                if (_state.RequestedGold > 0)
                    await _commandBus.SendAsync(new AddGold(_state.FromCharacterId, _state.RequestedGold), new CommandContext { CancellationToken = cancellationToken });

                _state.TradeStatus = TradeSagaStatus.Completed;
                _state.Status = SagaStatus.Completed;
            }
            catch (Exception ex)
            {
                // Откат: возвращаем то, что уже было списано (компенсация)
                await CompensateTrade(_state);
                _state.TradeStatus = TradeSagaStatus.Failed;
                _state.Status = SagaStatus.Failed;
                _state.FailureReason = ex.Message;
                await _eventBus.PublishAsync(new TradeFailed(_state.OfferId, ex.Message), cancellationToken);
            }
        }

        private void OnTradeOfferDeclined(TradeOfferDeclined e)
        {
            _state.TradeStatus = TradeSagaStatus.Declined;
            _state.Status = SagaStatus.Cancelled;
        }

        private void OnTradeOfferCancelled(TradeOfferCancelled e)
        {
            _state.TradeStatus = TradeSagaStatus.Cancelled;
            _state.Status = SagaStatus.Cancelled;
        }

        // ---------- Компенсация (откат) ----------

        private async Task CompensateTrade(TradeSagaState state)
        {
            // Возвращаем предметы и золото сторонам сделки, если они были списаны.
            // Реальная система идемпотентности тут нужна доработка, чтобы дважды не возвращать одно и то же
            // (актуально при повторном вызове компенсации после сбоя).
            foreach (var item in state.OfferedItems)
                await _commandBus.SendAsync(new AddInventoryItem(state.FromCharacterId, item.ItemId, item.ItemName, item.Quantity));
            if (state.OfferedGold > 0)
                await _commandBus.SendAsync(new AddGold(state.FromCharacterId, state.OfferedGold));

            foreach (var item in state.RequestedItems)
                await _commandBus.SendAsync(new AddInventoryItem(state.ToCharacterId, item.ItemId, item.ItemName, item.Quantity));
            if (state.RequestedGold > 0)
                await _commandBus.SendAsync(new AddGold(state.ToCharacterId, state.RequestedGold));
        }

        // ---------- Состояние саги ----------

        private class TradeSagaState : ISagaState
        {
            public Guid SagaId { get; set; }
            public Guid CorrelationId { get; set; }
            public SagaStatus Status { get; set; } = SagaStatus.Started;
            public int Version { get; set; }
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
            public DateTime? UpdatedAt { get; set; }

            public Guid OfferId { get; set; }
            public Guid FromCharacterId { get; set; }
            public Guid ToCharacterId { get; set; }
            public List<Events.TradeItem> OfferedItems { get; set; } = [];
            public int OfferedGold { get; set; }
            public List<Events.TradeItem> RequestedItems { get; set; } = [];
            public int RequestedGold { get; set; }
            public TradeSagaStatus TradeStatus { get; set; } = TradeSagaStatus.Pending;
            public string? FailureReason { get; set; }
        }

        private enum TradeSagaStatus
        {
            Pending,
            InProgress,
            Completed,
            Failed,
            Declined,
            Cancelled
        }
    }
}
