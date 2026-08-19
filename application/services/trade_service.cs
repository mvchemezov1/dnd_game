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
    /// Содержит информацию о товаре, его цене и редкости.
    /// </summary>
    public class TradeItem
    {
        /// <summary>Идентификатор предмета (уникальный строковый код).</summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>Название предмета для отображения.</summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>Количество единиц предмета.</summary>
        public int Quantity { get; set; }

        /// <summary>Базовая цена за единицу в золотых монетах (по PHB/DMG).</summary>
        public int BasePriceGold { get; set; }

        /// <summary>Является ли предмет магическим.</summary>
        public bool IsMagical { get; set; }

        /// <summary>Редкость предмета: Common, Uncommon, Rare, Very Rare, Legendary.</summary>
        public string Rarity { get; set; } = "Common";
    }

    /// <summary>
    /// Предложение обмена между двумя персонажами (торговля между игроками).
    /// Содержит список предлагаемых и запрашиваемых предметов, а также золото.
    /// </summary>
    public class TradeOffer
    {
        /// <summary>Уникальный идентификатор предложения.</summary>
        public Guid OfferId { get; set; }

        /// <summary>Идентификатор персонажа, который делает предложение.</summary>
        public Guid FromCharacterId { get; set; }

        /// <summary>Идентификатор персонажа, которому адресовано предложение.</summary>
        public Guid ToCharacterId { get; set; }

        /// <summary>Список предметов, предлагаемых первой стороной.</summary>
        public List<TradeItem> OfferedItems { get; set; } = [];

        /// <summary>Количество золота, предлагаемое первой стороной.</summary>
        public int OfferedGold { get; set; }

        /// <summary>Список предметов, запрашиваемых первой стороной.</summary>
        public List<TradeItem> RequestedItems { get; set; } = [];

        /// <summary>Количество золота, запрашиваемое первой стороной.</summary>
        public int RequestedGold { get; set; }

        /// <summary>Время создания предложения (UTC).</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Текущий статус предложения.</summary>
        public TradeOfferStatus Status { get; set; } = TradeOfferStatus.Pending;
    }

    /// <summary>
    /// Статус торгового предложения между игроками.
    /// </summary>
    public enum TradeOfferStatus
    {
        /// <summary>Ожидает ответа второй стороны.</summary>
        Pending,

        /// <summary>Принято второй стороной.</summary>
        Accepted,

        /// <summary>Отклонено второй стороной.</summary>
        Declined,

        /// <summary>Отменено первой стороной до ответа.</summary>
        Cancelled
    }

    /// <summary>
    /// Репозиторий для получения информации о ценах и скидках при торговле с NPC.
    /// Предоставляет базовые цены и модификаторы покупки/продажи, зависящие от NPC и персонажа.
    /// </summary>
    public interface ITradeRepository
    {
        /// <summary>
        /// Получить информацию о предмете по его идентификатору.
        /// </summary>
        /// <param name="itemId">Идентификатор предмета.</param>
        /// <returns>Объект <see cref="TradeItem"/> или <c>null</c>, если предмет не найден.</returns>
        TradeItem? GetItemInfo(string itemId);

        /// <summary>
        /// Получить множитель цены при покупке у конкретного NPC для конкретного персонажа.
        /// Учитывает репутацию, навыки торговли и другие факторы.
        /// </summary>
        /// <param name="npcId">Идентификатор NPC-торговца.</param>
        /// <param name="characterId">Идентификатор персонажа-покупателя.</param>
        /// <returns>Коэффициент, на который умножается базовая цена.</returns>
        float GetBuyMultiplier(Guid npcId, Guid characterId);

        /// <summary>
        /// Получить множитель цены при продаже конкретному NPC для конкретного персонажа.
        /// Обычно меньше 1 (например, 0.5 — продажа за полцены).
        /// </summary>
        /// <param name="npcId">Идентификатор NPC-торговца.</param>
        /// <param name="characterId">Идентификатор персонажа-продавца.</param>
        /// <returns>Коэффициент, на который умножается базовая цена.</returns>
        float GetSellMultiplier(Guid npcId, Guid characterId);
    }

    /// <summary>
    /// Класс-заглушка для представления торговой сделки (может быть расширен в будущем).
    /// </summary>
    public class Trade
    {
        /// <summary>Идентификатор сделки.</summary>
        public Guid Id { get; set; }
        // другие свойства по необходимости
    }

    /// <summary>
    /// Репозиторий для управления торговыми предложениями между игроками.
    /// Отвечает за хранение, поиск и обновление предложений.
    /// </summary>
    public interface ITradeOfferRepository
    {
        /// <summary>Добавить новое предложение в хранилище.</summary>
        void Add(TradeOffer offer);

        /// <summary>Найти предложение по идентификатору.</summary>
        TradeOffer? GetById(Guid offerId);

        /// <summary>Обновить существующее предложение.</summary>
        void Update(TradeOffer offer);

        /// <summary>Удалить предложение из хранилища.</summary>
        void Remove(Guid offerId);
    }

    // ---------- Сервис торговли ----------

    /// <summary>
    /// Сервис торговли, предоставляющий функциональность покупки и продажи предметов
    /// как с NPC, так и между игроками. Проверяет права доступа, наличие ресурсов
    /// и выполняет операции через командную шину.
    /// </summary>
    /// <remarks>
    /// Паттерн: Application Service. Координирует бизнес-операции, делегируя изменения состояния
    /// агрегатам через команды. Все проверки прав выполняются с помощью <see cref="PermissionChecker"/>.
    /// </remarks>
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
        /// Проверяет права на управление персонажем, наличие предмета у торговца,
        /// достаточность золота и добавляет предмет в инвентарь, списывая золото.
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа-покупателя.</param>
        /// <param name="npcId">Идентификатор NPC-торговца.</param>
        /// <param name="itemId">Идентификатор покупаемого предмета.</param>
        /// <param name="quantity">Количество единиц (по умолчанию 1).</param>
        /// <exception cref="UnauthorizedAccessException">Если у пользователя нет прав на управление персонажем.</exception>
        /// <exception cref="InvalidOperationException">
        /// Если персонаж не найден, предмет не найден, недостаточно золота или другие ошибки.
        /// </exception>
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
        /// Проверяет права, наличие предмета в инвентаре, вычисляет цену продажи
        /// и обновляет инвентарь и золото.
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа-продавца.</param>
        /// <param name="npcId">Идентификатор NPC-торговца.</param>
        /// <param name="itemId">Идентификатор продаваемого предмета.</param>
        /// <param name="quantity">Количество единиц (по умолчанию 1).</param>
        /// <exception cref="UnauthorizedAccessException">Если нет прав на управление персонажем.</exception>
        /// <exception cref="InvalidOperationException">Если персонаж не найден, предмет не найден или недостаточно предмета для продажи.</exception>
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
        /// Проверяет, что предлагающий персонаж существует и имеет достаточно ресурсов
        /// для выполнения своей части сделки. Предложение сохраняется в репозитории.
        /// </summary>
        /// <param name="fromCharacterId">Идентификатор персонажа, делающего предложение.</param>
        /// <param name="toCharacterId">Идентификатор персонажа, которому адресовано предложение.</param>
        /// <param name="offeredItems">Список предметов, предлагаемых первой стороной.</param>
        /// <param name="offeredGold">Количество золота, предлагаемое первой стороной.</param>
        /// <param name="requestedItems">Список предметов, запрашиваемых первой стороной.</param>
        /// <param name="requestedGold">Количество золота, запрашиваемое первой стороной.</param>
        /// <returns>Созданное предложение <see cref="TradeOffer"/>.</returns>
        /// <exception cref="UnauthorizedAccessException">Если нет прав на управление предлагающим персонажем.</exception>
        /// <exception cref="InvalidOperationException">
        /// Если персонажи не найдены, недостаточно предметов или золота для предложения.
        /// </exception>
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
        /// Проверяет, что предложение действительно и ожидает ответа, что принимающий имеет права,
        /// и что у него достаточно ресурсов для выполнения своей части сделки.
        /// Затем выполняет атомарный обмен предметами и золотом между персонажами.
        /// </summary>
        /// <param name="offerId">Идентификатор предложения.</param>
        /// <exception cref="InvalidOperationException">Если предложение не найдено, не в статусе Pending или недостаточно ресурсов.</exception>
        /// <exception cref="UnauthorizedAccessException">Если пользователь не может управлять принимающим персонажем.</exception>
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

            // 1. Забираем у предлагающего (FromCharacterId) предлагаемые предметы и золото
            foreach (var item in offer.OfferedItems)
            {
                for (int i = 0; i < item.Quantity; i++)
                    await commandBus.SendAsync(new RemoveInventoryItem(offer.FromCharacterId, item.ItemId));
            }
            if (offer.OfferedGold > 0)
                await commandBus.SendAsync(new SpendGold(offer.FromCharacterId, offer.OfferedGold));

            // 2. Забираем у принимающего (ToCharacterId) запрашиваемые предметы и золото
            foreach (var item in offer.RequestedItems)
            {
                for (int i = 0; i < item.Quantity; i++)
                    await commandBus.SendAsync(new RemoveInventoryItem(offer.ToCharacterId, item.ItemId));
            }
            if (offer.RequestedGold > 0)
                await commandBus.SendAsync(new SpendGold(offer.ToCharacterId, offer.RequestedGold));

            // 3. Передаём принимающему предложенные предметы и золото
            foreach (var item in offer.OfferedItems)
            {
                await commandBus.SendAsync(new AddInventoryItem(offer.ToCharacterId, item.ItemId, item.ItemName, item.Quantity));
            }
            if (offer.OfferedGold > 0)
                await commandBus.SendAsync(new AddGold(offer.ToCharacterId, offer.OfferedGold));

            // 4. Передаём предлагающему запрошенные предметы и золото
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
        /// Может выполнить только персонаж, которому адресовано предложение.
        /// </summary>
        /// <param name="offerId">Идентификатор предложения.</param>
        /// <exception cref="InvalidOperationException">Если предложение не найдено или не в статусе Pending.</exception>
        /// <exception cref="UnauthorizedAccessException">Если пользователь не может управлять принимающим персонажем.</exception>
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
        /// Отменить своё предложение (до того, как его приняли или отклонили).
        /// Может выполнить только персонаж, создавший предложение.
        /// </summary>
        /// <param name="offerId">Идентификатор предложения.</param>
        /// <exception cref="InvalidOperationException">Если предложение не найдено или не в статусе Pending.</exception>
        /// <exception cref="UnauthorizedAccessException">Если пользователь не может управлять предлагающим персонажем.</exception>
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
        /// Получить количество золота у персонажа.
        /// Заглушка: в реальной системе нужно иметь соответствующее поле в CharacterProjection или команду GetGold.
        /// В текущей реализации возвращает значение из DTO персонажа (<see cref="CharacterDto.Gold"/>).
        /// </summary>
        /// <param name="characterId">Идентификатор персонажа.</param>
        /// <returns>Количество золота у персонажа (0, если персонаж не найден).</returns>
        private async Task<int> GetCharacterGold(Guid characterId)
        {
            var character = await characterProjection.GetById(characterId);
            return character?.Gold ?? 0;
        }
    }
}