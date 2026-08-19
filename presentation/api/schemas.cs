// presentation/api/schemas.cs
using dnd_game.Application.Services;
using dnd_game.Domain.Events;

namespace dnd_game.Presentation.Api
{
    /// <summary>
    /// Контейнер для всех схем данных API (запросы/ответы), используемых в REST-интерфейсе игры.
    /// </summary>
    public static class Schemas
    {
        // --------------------------------------------------------------------------------
        // Персонаж
        // --------------------------------------------------------------------------------
        public record CreateCharacterRequest(string Name, int MaxHitPoints);
        public record UpdateCharacterRequest(string? Name, int? MaxHitPoints);
        public record HealCharacterRequest(int Amount);
        public record SetTemporaryHitPointsRequest(int Amount);
        public record GainExperienceRequest(int ExperiencePoints);
        public record LevelUpCharacterRequest(int NewLevel);
        public record SetAbilityScoreRequest(int Score);
        public record ChooseRaceRequest(string Race);
        public record ChooseClassRequest(string ClassName);
        public record ChooseBackgroundRequest(string BackgroundName);
        public record AddFeatRequest(string FeatName);
        public record AddSpellRequest(string SpellId);
        public record PrepareSpellRequest(string SpellId);
        public record UseSpellSlotRequest(int SlotLevel);
        public record AddInventoryItemRequest(string ItemId, string ItemName, int Quantity = 1);
        public record RemoveInventoryItemRequest(string ItemId, int Quantity = 1);
        public record EquipItemRequest(string ItemId, string Slot, string ItemName, int ArmorBonus = 0, int DamageBonus = 0);
        public record UnequipItemRequest(string ItemId);
        public record DeathSavingThrowRequest(bool Success);
        public record StartRestRequest(string RestType);
        public record MoveCharacterRequest(int TargetX, int TargetY);
        public record UpdateArmorClassRequest(int NewArmorClass);
        public record UpdateSpeedRequest(int NewSpeed);

        // --------------------------------------------------------------------------------
        // Бой
        // -------------------------------------------------------------------------------

        // --------------------------------------------------------------------------------
        // Ответы (DTO)
        // --------------------------------------------------------------------------------
        public record CharacterDto(
            Guid Id,
            string Name,
            int HitPoints,
            int MaxHitPoints,
            int TemporaryHitPoints,
            int ArmorClass,
            int Speed,
            int ExperiencePoints,
            int Level,
            string Race,
            string Class,
            string Background,
            int ProficiencyBonus,
            Dictionary<string, int> AbilityScores,
            List<string> SkillProficiencies,
            List<string> SavingThrowProficiencies,
            List<string> KnownSpells,
            Dictionary<int, int> MaxSpellSlots,
            Dictionary<int, int> UsedSpellSlots,
            Dictionary<int, int> HitDiceRemaining,
            Dictionary<int, int> MaxHitDice,
            int DeathSaveSuccesses,
            int DeathSaveFailures,
            bool IsStable,
            bool IsDead,
            List<string> Conditions,
            List<string> Resistances,
            List<string> Vulnerabilities,
            List<string> Immunities,
            List<EquippedItemDto> Equipment,
            List<InventoryItemDto> Inventory,
            List<string> Feats,
            bool Concentrating,
            int Gold
        );

        public record CreateQuestRequest(
            Guid QuestId,
            string Title,
            List<QuestObjectiveData> Objectives,
            List<QuestRewardData> Rewards,
            List<Guid> ParticipantIds
        );

        public record UpdateQuestObjectiveRequest(
            int ObjectiveIndex,
            bool IsCompleted,
            int CurrentProgress
        );

        public record EquippedItemDto(string ItemId, string Slot, string Name, int ArmorBonus, int DamageBonus);
        public record InventoryItemDto(string ItemId, string Name, int Quantity);
        public record CharacterHitPointsDto(int Current, int Max, int Temporary);
        public record CharacterCombatStatsDto(int ArmorClass, int Speed, Dictionary<int, int> HitDiceRemaining, int DeathSaveSuccesses, int DeathSaveFailures, bool IsStable);
        public record CharacterSpellsDto(List<string> KnownSpells, Dictionary<int, int> MaxSpellSlots, Dictionary<int, int> UsedSpellSlots);
        public record CharacterDeathStatusDto(string Status, int DeathSaveSuccesses, int DeathSaveFailures);
        public record CharacterDefensesDto(List<string> Resistances, List<string> Vulnerabilities, List<string> Immunities);
        public record CharacterSummaryDto(Guid Id, string Name, int Level, string Class, string Race, int HitPoints, int MaxHitPoints, bool IsAlive, int ArmorClass);

        public record CombatStatusDto(Guid CombatId, bool IsActive, int Round, int CurrentTurnIndex, string CurrentTurnCharacterName, List<CombatParticipantDto> Participants);
        public record CombatParticipantDto(Guid CharacterId, string Name, int Initiative, int CurrentHitPoints, int MaxHitPoints, int TemporaryHitPoints, int ArmorClass, int MovementRemaining, bool HasAction, bool HasBonusAction, bool HasReaction, List<string> Conditions, bool Concentrating, string DeathStatus, int DeathSaveSuccesses, int DeathSaveFailures);

        public record CampaignStateDto(Guid CampaignId, string CampaignName, int CurrentAct, int Day, int Hour, int Minute, string Weather, List<string> DiscoveredRegions, Dictionary<string, string> GlobalFlags);
        public record QuestInfoDto(Guid QuestId, string Title, string Status, List<QuestObjectiveDto> Objectives, List<QuestRewardDto> Rewards);
        public record QuestObjectiveDto(string Description, bool IsCompleted, int CurrentProgress, int RequiredProgress);
        public record QuestRewardDto(string Description, int ExperiencePoints, int Gold, List<string> ItemIds, string? FactionReputationChange);

        // Внутри класса CombatController (можно в самом конце, перед закрывающей скобкой)

        public record StartCombatRequest(Guid CombatId, List<Guid> Participants);
        public record RollInitiativeRequest(Guid ParticipantId, int InitiativeRoll, int DexterityModifier);
        public record AddParticipantRequest(Guid ParticipantId, int Initiative);
        public record TakeMoveActionRequest(Guid ParticipantId, int DistanceFeet);
        public record TakeStandardActionRequest(Guid ParticipantId, string ActionType, Guid? TargetId, object? ActionData);
        public record TakeBonusActionRequest(Guid ParticipantId, string ActionType, Guid? TargetId, object? ActionData);
        public record TakeReactionRequest(Guid ParticipantId, string ReactionType, string TriggerDescription, Guid? TargetId);
        public record ReadyActionRequest(Guid ParticipantId, string ActionToReady, string TriggerCondition);
        public record TriggerReadyActionRequest(Guid ParticipantId);
        public record DealDamageRequest(Guid SourceParticipantId, Guid TargetParticipantId, int DamageAmount, string DamageType);
        public record HealTargetRequest(Guid SourceParticipantId, Guid TargetParticipantId, int HealingAmount);
        public record ApplyConditionRequest(Guid TargetParticipantId, string ConditionType, int DurationRounds);
        public record RemoveConditionRequest(Guid TargetParticipantId, string ConditionType);
        public record MakeSavingThrowRequest(Guid ParticipantId, string Ability, int DifficultyClass, int RollResult, int Modifiers);
        public record MakeDeathSavingThrowRequest(Guid ParticipantId, int RollResult);
        public record StabilizeRequest(Guid ParticipantId, Guid StabilizedByParticipantId);
        public record MakeConcentrationCheckRequest(Guid ParticipantId, int DifficultyClass, int RollResult, int ConstitutionModifier);
        public record DelayTurnRequest(Guid ParticipantId);
        public record SurrenderRequest(Guid ParticipantId);
        public record PerformActionRequest(Guid ParticipantId, string ActionType, Guid? TargetId = null, object? ActionData = null);

        // ---------- Сообщения WebSocket ----------

        public record WebSocketMessageBase(string Type, string? CorrelationId);

        public record AuthRequestMessage(string Token) : WebSocketMessageBase("auth", null);
        public record AuthResponseMessage(bool Success, Guid? UserId, string? Error) : WebSocketMessageBase("auth_response", null);

        public record CommandMessage(string CommandType, string CommandJson) : WebSocketMessageBase("command", null);
        public record CommandResponseMessage(bool Success, string? ErrorMessage, string? ResultJson) : WebSocketMessageBase("command_response", null);

        public record EventMessage(string EventType, string EventJson) : WebSocketMessageBase("event", null);
        public record ErrorMessage(string ErrorCode, string Message, string? Detail) : WebSocketMessageBase("error", null);

        public record PingMessage() : WebSocketMessageBase("ping", null);
        public record PongMessage() : WebSocketMessageBase("pong", null);
        public record AddGoldRequest(int Amount);
        public record SpendGoldRequest(int Amount);
        public record SetGoldRequest(int Amount);
        public record ClearAllConditions(Guid id);
        // ---- Crafting ----
        public record StartCraftingRequest(Guid CharacterId, Guid RecipeId);
        public record CancelCraftingRequest(Guid ProcessId);

        // ---- Trade ----
        public record ProposeTradeRequest(
            Guid FromCharacterId,
            Guid ToCharacterId,
            List<Domain.Events.TradeItem> OfferedItems,
            int OfferedGold,
            List<Domain.Events.TradeItem> RequestedItems,
            int RequestedGold
        );
        public record AcceptTradeRequest(Guid OfferId);
        public record DeclineTradeRequest(Guid OfferId);
        public record CancelTradeOfferRequest(Guid OfferId);

        // ---- Dialog ----
        public record StartDialogRequest(Guid DialogueId, Guid NpcId, Guid CharacterId);
        public record SelectOptionRequest(Guid DialogueId, Guid OptionId);
        public record EndDialogRequest(Guid DialogueId);

        // ---- Travel ----
        public record DashRequest(Guid CharacterId);
        public record SpecialMovementRequest(Guid CharacterId, int DistanceFeet, string MovementType);
        public record StartJourneyRequest(Guid PartyId, Guid RouteId, TravelPace Pace);
        public record EndJourneyRequest(Guid PartyId);
        public record TravelDayRequest(Guid PartyId, TerrainType Terrain, int HoursTraveled, int NavigationCheckResult);
    }
}