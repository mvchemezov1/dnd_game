// application/services/trade_service.cs
using dnd_game.Domain.Commands;
using dnd_game.Application.Projections;          // CharacterProjection
using dnd_game.Application.Security;            // PermissionChecker
using dnd_game.infrastructure.message_bus;       // ICommandBus

namespace dnd_game.Application.Services
{
    // ---------- Модели для торговли ----------

    /// <summary>
    /// Предмет, участвующий в торговой сделке.
    /// </summary>
    public class TradeItem
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int BasePriceGold { get; set; }          // базовая цена за единицу (по PHB/DMG)
        public bool IsMagical { get; set; }
        public string Rarity { get; set; } = "Common";  // Common, Uncommon, Rare, Very Rare, Legendary
    }

    /// <summary>
    /// Предложение обмена (для торговли между игроками).
    /// </summary>
    public class TradeOffer
    {
        public Guid OfferId { get; set; }
        public Guid FromCharacterId { get; set; }
        public Guid ToCharacterId { get; set; }
        public List<TradeItem> OfferedItems { get; set; } = [];
        public int OfferedGold { get; set; }
        public List<TradeItem> RequestedItems { get; set; } = [];
        public int RequestedGold { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public TradeOfferStatus Status { get; set; } = TradeOfferStatus.Pending;
    }

    public enum TradeOfferStatus
    {
        Pending,
        Accepted,
        Declined,
        Cancelled
    }

    /// <summary>
    /// Репозиторий для получения цен и скидок от NPC.
    /// </summary>
    public interface ITradeRepository
    {
        TradeItem? GetItemInfo(string itemId);
        float GetBuyMultiplier(Guid npcId, Guid characterId);   // коэффициент к базовой цене при покупке
        float GetSellMultiplier(Guid npcId, Guid characterId);  // коэффициент при продаже
    }

    public class Trade
    {
        public Guid Id { get; set; }
        // другие свойства по необходимости
    }

    /// <summary>
    /// Репозиторий активных торговых предложений между игроками.
    /// </summary>
    public interface ITradeOfferRepository
    {
        void Add(TradeOffer offer);
        TradeOffer? GetById(Guid offerId);
        void Update(TradeOffer offer);
        void Remove(Guid offerId);
    }

    // ---------- Сервис торговли ----------

    public class TradeService(
        ICommandBus commandBus,
        CharacterProjection characterProjection,
        PermissionChecker permissionChecker,
        ITradeRepository tradeRepo,
        ITradeOfferRepository offerRepo)
    {

        // ---------- Торговля с NPC ----------

        /// <summary>
        /// Персонаж покупает предмет у NPC-торговца.
        /// </summary>
        public async Task BuyItemFromNpc(Guid characterId, Guid npcId, string itemId, int quantity = 1)
        {
            // Проверка прав: игрок может управлять своим персонажем
            if (!permissionChecker.CanControlCharacter(characterId))
                throw new UnauthorizedAccessException("You cannot control this character.");
            _ = await characterProjection.GetById(characterId)
                            ?? throw new InvalidOperationException("Character not found.");

            var itemInfo = tradeRepo.GetItemInfo(itemId)
                           ?? throw new InvalidOperationException("Item not found in trade repository.");

            // Определяем итоговую цену с учётом репутации, навыков, скидок
            float buyMultiplier = tradeRepo.GetBuyMultiplier(npcId, characterId);
            int totalCostGold = (int)(itemInfo.BasePriceGold * quantity * buyMultiplier);

            // Проверка наличия золота у персонажа
            int characterGold = await GetCharacterGold(characterId);
            if (characterGold < totalCostGold)
                throw new InvalidOperationException($"Not enough gold. Required: {totalCostGold}, available: {characterGold}.");

            // Проверка вместимости инвентаря (упрощённо, без веса)
            // Можно добавить проверку через CharacterProjection, если есть MaxInventorySlots

            // Выполняем транзакцию: снять золото, добавить предмет
            await commandBus.SendAsync(new SpendGold(characterId, totalCostGold));
            await commandBus.SendAsync(new AddInventoryItem(characterId, itemId, itemInfo.ItemName, quantity));

            // Опционально: событие о покупке
            // await _commandBus.SendAsync(new ItemPurchasedFromNpc(characterId, npcId, itemId, quantity, totalCostGold));
        }

        /// <summary>
        /// Персонаж продаёт предмет NPC-торговцу.
        /// </summary>
        public async Task SellItemToNpc(Guid characterId, Guid npcId, string itemId, int quantity = 1)
        {
            if (!permissionChecker.CanControlCharacter(characterId))
                throw new UnauthorizedAccessException("You cannot control this character.");

            var character = await characterProjection.GetById(characterId)
                            ?? throw new InvalidOperationException("Character not found.");

            var itemInfo = tradeRepo.GetItemInfo(itemId)
                           ?? throw new InvalidOperationException("Item not found in trade repository.");

            // Проверяем наличие предмета у персонажа в нужном количестве
            var inventoryItem = character.Inventory.FirstOrDefault(i => i.ItemId == itemId);
            if (inventoryItem == null || inventoryItem.Quantity < quantity)
                throw new InvalidOperationException("Character does not have enough of this item to sell.");

            // Итоговая цена продажи (обычно половина базовой, если не указано иное, с модификатором)
            float sellMultiplier = tradeRepo.GetSellMultiplier(npcId, characterId);
            int totalGold = (int)(itemInfo.BasePriceGold * quantity * sellMultiplier);

            // Удаляем предмет, добавляем золото
            for (int i = 0; i < quantity; i++)
                await commandBus.SendAsync(new RemoveInventoryItem(characterId, itemId));
            await commandBus.SendAsync(new AddGold(characterId, totalGold));

            // Событие
            // await _commandBus.SendAsync(new ItemSoldToNpc(characterId, npcId, itemId, quantity, totalGold));
        }

        // ---------- Торговля между игроками ----------

        /// <summary>
        /// Создать предложение обмена между двумя персонажами.
        /// </summary>
        public async Task<TradeOffer> ProposeTrade(Guid fromCharacterId, Guid toCharacterId, List<TradeItem> offeredItems, int offeredGold, List<TradeItem> requestedItems, int requestedGold)
        {
            if (!permissionChecker.CanControlCharacter(fromCharacterId))
                throw new UnauthorizedAccessException("You cannot control the offering character.");

            var fromChar = await characterProjection.GetById(fromCharacterId)
                           ?? throw new InvalidOperationException("Offering character not found.");
            var toChar = await characterProjection.GetById(toCharacterId)
                         ?? throw new InvalidOperationException("Receiving character not found.");

            // Проверяем, что предлагающий действительно имеет предлагаемые предметы и золото
            foreach (var item in offeredItems)
            {
                var invItem = fromChar.Inventory.FirstOrDefault(i => i.ItemId == item.ItemId);
                if (invItem == null || invItem.Quantity < item.Quantity)
                    throw new InvalidOperationException($"You don't have enough of {item.ItemName} to offer.");
            }
            int fromGold = await GetCharacterGold(fromCharacterId);
            if (fromGold < offeredGold)
                throw new InvalidOperationException("Not enough gold to offer.");

            // Создаём предложение
            var offer = new TradeOffer
            {
                OfferId = Guid.NewGuid(),
                FromCharacterId = fromCharacterId,
                ToCharacterId = toCharacterId,
                OfferedItems = offeredItems,
                OfferedGold = offeredGold,
                RequestedItems = requestedItems,
                RequestedGold = requestedGold,
                Status = TradeOfferStatus.Pending
            };
            offerRepo.Add(offer);
            return offer;
        }

        /// <summary>
        /// Принять предложение обмена.
        /// </summary>
        public async Task AcceptTrade(Guid offerId)
        {
            var offer = offerRepo.GetById(offerId)
                        ?? throw new InvalidOperationException("Trade offer not found.");
            if (offer.Status != TradeOfferStatus.Pending)
                throw new InvalidOperationException("Trade offer is not pending.");

            // Проверка прав: тот, кто принимает, должен иметь контроль над ToCharacterId
            if (!permissionChecker.CanControlCharacter(offer.ToCharacterId))
                throw new UnauthorizedAccessException("You cannot control the receiving character.");

            var toChar = await characterProjection.GetById(offer.ToCharacterId)
                         ?? throw new InvalidOperationException("Receiving character not found.");

            // Проверяем наличие запрошенных предметов и золота у принимающего
            foreach (var item in offer.RequestedItems)
            {
                var invItem = toChar.Inventory.FirstOrDefault(i => i.ItemId == item.ItemId);
                if (invItem == null || invItem.Quantity < item.Quantity)
                    throw new InvalidOperationException($"You don't have enough of {item.ItemName} to complete the trade.");
            }
            int toGold = await GetCharacterGold(offer.ToCharacterId);
            if (toGold < offer.RequestedGold)
                throw new InvalidOperationException("Not enough gold to complete the trade.");

            // Атомарно проводим обмен: отправляем команды на перемещение предметов и золота
            // Сначала забираем у предлагающего
            foreach (var item in offer.OfferedItems)
            {
                for (int i = 0; i < item.Quantity; i++)
                    await commandBus.SendAsync(new RemoveInventoryItem(offer.FromCharacterId, item.ItemId));
            }
            if (offer.OfferedGold > 0)
                await commandBus.SendAsync(new SpendGold(offer.FromCharacterId, offer.OfferedGold));

            // Отдаём предлагающему запрошенное
            foreach (var item in offer.RequestedItems)
            {
                for (int i = 0; i < item.Quantity; i++)
                    await commandBus.SendAsync(new RemoveInventoryItem(offer.ToCharacterId, item.ItemId));
            }
            if (offer.RequestedGold > 0)
                await commandBus.SendAsync(new SpendGold(offer.ToCharacterId, offer.RequestedGold));

            // Даём принимающему предложенные предметы и золото
            foreach (var item in offer.OfferedItems)
            {
                await commandBus.SendAsync(new AddInventoryItem(offer.ToCharacterId, item.ItemId, item.ItemName, item.Quantity));
            }
            if (offer.OfferedGold > 0)
                await commandBus.SendAsync(new AddGold(offer.ToCharacterId, offer.OfferedGold));

            // Даём предлагающему запрошенные предметы и золото
            foreach (var item in offer.RequestedItems)
            {
                await commandBus.SendAsync(new AddInventoryItem(offer.FromCharacterId, item.ItemId, item.ItemName, item.Quantity));
            }
            if (offer.RequestedGold > 0)
                await commandBus.SendAsync(new AddGold(offer.FromCharacterId, offer.RequestedGold));

            // Обновляем статус предложения
            offer.Status = TradeOfferStatus.Accepted;
            offerRepo.Update(offer);

            // Событие
            // await _commandBus.SendAsync(new TradeCompleted(offer));
        }

        /// <summary>
        /// Отклонить предложение обмена.
        /// </summary>
        public Task DeclineTrade(Guid offerId)
        {
            var offer = offerRepo.GetById(offerId)
                        ?? throw new InvalidOperationException("Trade offer not found.");
            if (offer.Status != TradeOfferStatus.Pending)
                throw new InvalidOperationException("Trade offer is not pending.");

            if (!permissionChecker.CanControlCharacter(offer.ToCharacterId))
                throw new UnauthorizedAccessException("You cannot decline this offer.");

            offer.Status = TradeOfferStatus.Declined;
            offerRepo.Update(offer);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Отменить своё предложение (до того, как его приняли/отклонили).
        /// </summary>
        public Task CancelTradeOffer(Guid offerId)
        {
            var offer = offerRepo.GetById(offerId)
                        ?? throw new InvalidOperationException("Trade offer not found.");
            if (offer.Status != TradeOfferStatus.Pending)
                throw new InvalidOperationException("Trade offer is not pending.");

            if (!permissionChecker.CanControlCharacter(offer.FromCharacterId))
                throw new UnauthorizedAccessException("You cannot cancel this offer.");

            offer.Status = TradeOfferStatus.Cancelled;
            offerRepo.Update(offer);
            return Task.CompletedTask;
        }

        // ---------- Вспомогательные методы ----------

        /// <summary>
        /// Получить количество золота у персонажа. Заглушка: в реальной системе нужно иметь соответствующее поле в CharacterProjection или команду GetGold.
        /// </summary>
        private async Task<int> GetCharacterGold(Guid characterId)
        {
            // Через проекцию или отдельный запрос
            var character = await characterProjection.GetById(characterId);
            // Предположим, что в CharacterDto есть поле Gold (можно добавить). Пока заглушка 0.
            return character?.Gold ?? 0;
        }
    }
}