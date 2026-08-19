namespace dnd_game.Domain.Events;

public record TradeItem(string ItemId, string ItemName, int Quantity);

public record TradeOfferCreated(
    Guid OfferId,
    Guid FromCharacterId,
    Guid ToCharacterId,
    List<TradeItem> OfferedItems,
    int OfferedGold,
    List<TradeItem> RequestedItems,
    int RequestedGold,
    DateTime OccurredOn
) : IDomainEvent;

public record TradeOfferAccepted(Guid OfferId, DateTime OccurredOn) : IDomainEvent;
public record TradeOfferDeclined(Guid OfferId, DateTime OccurredOn) : IDomainEvent;
public record TradeOfferCancelled(Guid OfferId, DateTime OccurredOn) : IDomainEvent;
public record TradeItemTransferred(Guid OfferId, Guid CharacterId, string ItemId, int Quantity) : IDomainEvent;
public record TradeGoldTransferred(Guid OfferId, Guid CharacterId, int Amount) : IDomainEvent;
public record TradeFailed(Guid OfferId, string Reason) : IDomainEvent;