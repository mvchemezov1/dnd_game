// presentation/dm_tools/override_commands.cs
using dnd_game.Domain.Commands;
using dnd_game.infrastructure.message_bus;

namespace dnd_game.Presentation.DmTools;

/// <summary>
/// Инструменты Мастера для принудительного изменения состояния игры,
/// обхода ограничений и быстрого применения эффектов.
/// Все методы отправляют соответствующие доменные команды через шину команд.
/// </summary>
public class OverrideCommands
{
    private readonly ICommandBus _commandBus;

    public OverrideCommands(ICommandBus commandBus) => _commandBus = commandBus;

    // --------------------------------------------------------------------------------
    // Жизнь и смерть персонажа
    // --------------------------------------------------------------------------------
    public async Task ForceKill(Guid characterId)
    {
        await _commandBus.SendAsync(new MarkCharacterDead(characterId));
    }

    public async Task ReviveCharacter(Guid characterId, int newHitPoints = 1)
    {
        await _commandBus.SendAsync(new ReviveCharacter(characterId, newHitPoints));
    }

    // --------------------------------------------------------------------------------
    // Предметы и золото
    // --------------------------------------------------------------------------------
    public async Task GrantItem(Guid characterId, string itemId, string itemName, int quantity = 1)
    {
        await _commandBus.SendAsync(new AddInventoryItem(characterId, itemId, itemName, quantity));
    }

    public async Task RemoveItem(Guid characterId, string itemId, int quantity = 1)
    {
        await _commandBus.SendAsync(new RemoveInventoryItem(characterId, itemId, quantity));
    }

    public async Task GrantGold(Guid characterId, int amount)
    {
        await _commandBus.SendAsync(new AddGold(characterId, amount));
    }

    public async Task SetGold(Guid characterId, int amount)
    {
        // Для установки точной суммы золота – отдельной команды может не быть.
        // Можно выполнить RemoveAllGold + AddGold, либо создать отдельную команду SetGold.
        await _commandBus.SendAsync(new SetGoldCommand(characterId, amount));
    }

    // --------------------------------------------------------------------------------
    // Характеристики и уровень
    // --------------------------------------------------------------------------------
    public async Task SetAbilityScore(Guid characterId, string ability, int score)
    {
        await _commandBus.SendAsync(new SetAbilityScore(characterId, ability, score));
    }

    public async Task SetLevel(Guid characterId, int newLevel)
    {
        await _commandBus.SendAsync(new LevelUpCharacter(characterId, newLevel));
    }

    public async Task GrantExperience(Guid characterId, int amount)
    {
        await _commandBus.SendAsync(new GainExperience(characterId, amount));
    }

    // --------------------------------------------------------------------------------
    // Состояния
    // --------------------------------------------------------------------------------
    public async Task ApplyCondition(Guid characterId, string condition, int durationRounds = 1)
    {
        await _commandBus.SendAsync(new ApplyCondition(characterId, condition, durationRounds));
    }

    public async Task RemoveCondition(Guid characterId, string condition)
    {
        await _commandBus.SendAsync(new RemoveCondition(characterId, condition));
    }

    public async Task ClearAllConditions(Guid characterId)
    {
        // Для простоты отправляем последовательные команды.
        // В реальности можно получить список состояний из проекции и удалить каждое.
        // Но здесь используем заглушку: отправляем RemoveCondition для всех стандартных условий.
        // Однако без знания состояний можно реализовать команду ClearAllConditions.
        await _commandBus.SendAsync(new ClearAllConditionsCommand(characterId));
    }

    // --------------------------------------------------------------------------------
    // Перемещение и телепортация
    // --------------------------------------------------------------------------------
    public async Task TeleportCharacter(Guid characterId, int x, int y)
    {
        await _commandBus.SendAsync(new TeleportCommand(characterId, x, y));
    }

    public async Task MoveCharacter(Guid characterId, int x, int y)
    {
        await _commandBus.SendAsync(new MoveCharacter(characterId, x, y));
    }

    // --------------------------------------------------------------------------------
    // Бой
    // --------------------------------------------------------------------------------
    public async Task StartCombat(Guid combatId, List<Guid> participants)
    {
        await _commandBus.SendAsync(new StartCombat(combatId, participants));
    }

    public async Task EndCombat(Guid combatId)
    {
        await _commandBus.SendAsync(new EndCombat(combatId));
    }

    public async Task AddToCombat(Guid combatId, Guid participantId, int initiative)
    {
        await _commandBus.SendAsync(new AddParticipantToCombat(combatId, participantId, initiative));
    }

    public async Task RemoveFromCombat(Guid combatId, Guid participantId)
    {
        await _commandBus.SendAsync(new RemoveParticipantFromCombat(combatId, participantId));
    }

    // --------------------------------------------------------------------------------
    // Кампания и глобальные флаги
    // --------------------------------------------------------------------------------
    public async Task SetGlobalFlag(Guid campaignId, string flagName, string value)
    {
        await _commandBus.SendAsync(new SetGlobalFlagCommand(campaignId, flagName, value));
    }

    public async Task RemoveGlobalFlag(Guid campaignId, string flagName)
    {
        await _commandBus.SendAsync(new RemoveGlobalFlagCommand(campaignId, flagName));
    }

    public async Task ChangeFactionReputation(Guid characterId, string factionId, int change)
    {
        // Команда ChangeFactionReputation может требовать campaignId, но есть вариант с Guid.Empty
        await _commandBus.SendAsync(new ChangeFactionReputation(characterId, factionId, change));
    }

    public async Task CompleteQuest(Guid campaignId, Guid questId)
    {
        await _commandBus.SendAsync(new CompleteQuestCommand(campaignId, questId));
    }

    public async Task FailQuest(Guid campaignId, Guid questId)
    {
        await _commandBus.SendAsync(new FailQuestCommand(campaignId, questId));
    }

    // --------------------------------------------------------------------------------
    // Время и погода
    // --------------------------------------------------------------------------------
    public async Task AdvanceTime(Guid campaignId, int minutes)
    {
        await _commandBus.SendAsync(new AdvanceTimeCommand(campaignId, minutes));
    }

    public async Task ChangeWeather(Guid campaignId, string weather)
    {
        await _commandBus.SendAsync(new ChangeWeatherCommand(campaignId, weather));
    }

    // --------------------------------------------------------------------------------
    // Спаун существ
    // --------------------------------------------------------------------------------
    public async Task SpawnMonster(string templateId, int x, int y, string name = "", int maxHp = 10)
    {
        var characterId = Guid.NewGuid();
        await _commandBus.SendAsync(new CreateCharacter(characterId, string.IsNullOrEmpty(name) ? templateId : name, maxHp));
        if (x != 0 || y != 0)
            await _commandBus.SendAsync(new MoveCharacter(characterId, x, y));
    }

    // --------------------------------------------------------------------------------
    // Прочее
    // --------------------------------------------------------------------------------
    public async Task ResetCharacter(Guid characterId)
    {
        // Сброс всех состояний, восстановление хитов, снятие эффектов
        await _commandBus.SendAsync(new ClearAllConditionsCommand(characterId));
        await _commandBus.SendAsync(new HealCharacter(characterId, 9999)); // до максимума
        await _commandBus.SendAsync(new ReviveCharacter(characterId, 1)); // если мёртв
    }
}