using dnd_game.Domain.Aggregates;
using dnd_game.Domain.Commands;
using dnd_game.Domain.Events;
using dnd_game.Domain.Exceptions;
using dnd_game.Infrastructure.EventStore;
using dnd_game.Application.CommandHandlers;

namespace dnd_game.application.command_handlers;

/// <summary>
/// Обрабатывает команды, связанные с персонажем, загружая агрегат <see cref="CharacterAggregate"/> из хранилища событий,
/// вызывая соответствующее поведение домена и сохраняя результирующие события.
/// Реализует паттерн обработчика команд с использованием событийного сорсинга.
/// </summary>
/// <remarks>
/// Каждый обработчик команды следует одному и тому же потоку:
/// 1. Загрузить агрегат по его идентификатору.
/// 2. Если агрегат не найден, выбросить исключение <see cref="InvalidAction"/>.
/// 3. Вызвать метод агрегата, соответствующий команде.
/// 4. Сохранить агрегат, что приводит к добавлению новых событий в хранилище событий.
/// </remarks>
public class CharacterHandler :
    ICommandHandler<CreateCharacter>,
    ICommandHandler<UpdateCharacter>,
    ICommandHandler<DealDamage>,
    ICommandHandler<HealCharacter>,
    ICommandHandler<SetTemporaryHitPoints>,
    ICommandHandler<GainExperience>,
    ICommandHandler<LevelUpCharacter>,
    ICommandHandler<SetAbilityScore>,
    ICommandHandler<AddGold>,
    ICommandHandler<SpendGold>,
    ICommandHandler<SetGoldCommand>,
    ICommandHandler<ClearAllConditionsCommand>,
    ICommandHandler<AddSpell>,
    ICommandHandler<RemoveSpell>,
    ICommandHandler<CastSpell>,
    ICommandHandler<TakeShortRest>,
    ICommandHandler<TakeLongRest>,
    ICommandHandler<ApplyCondition>,
    ICommandHandler<RemoveCondition>,
    ICommandHandler<MakeSavingThrow>,
    ICommandHandler<DeathSavingThrow>,
    ICommandHandler<StabilizeCharacter>,
    ICommandHandler<UpdateTemporaryHitPoints>,
    ICommandHandler<UpdateArmorClass>,
    ICommandHandler<UpdateSpeed>,
    ICommandHandler<UpdateProficiencyBonus>,
    ICommandHandler<AddInventoryItem>,
    ICommandHandler<RemoveInventoryItem>,
    ICommandHandler<EquipItem>,
    ICommandHandler<UnequipItem>,
    ICommandHandler<ChooseRace>,
    ICommandHandler<ChooseClass>,
    ICommandHandler<ChooseBackground>,
    ICommandHandler<AddSkillProficiency>,
    ICommandHandler<RemoveSkillProficiency>,
    ICommandHandler<AddSavingThrowProficiency>,
    ICommandHandler<RemoveSavingThrowProficiency>,
    ICommandHandler<AddFeat>,
    ICommandHandler<RemoveFeat>,
    ICommandHandler<PrepareSpell>,
    ICommandHandler<UnprepareSpell>,
    ICommandHandler<UseClassFeature>,
    ICommandHandler<RechargeFeature>,
    ICommandHandler<AttuneItem>,
    ICommandHandler<UnattuneItem>,
    ICommandHandler<AddResistance>,
    ICommandHandler<RemoveResistance>,
    ICommandHandler<AddVulnerability>,
    ICommandHandler<RemoveVulnerability>,
    ICommandHandler<AddImmunity>,
    ICommandHandler<RemoveImmunity>,
    ICommandHandler<ReviveCharacter>,
    ICommandHandler<ResetDeathSavingThrows>,
    ICommandHandler<UseSpellSlot>,
    ICommandHandler<RestoreAllSpellSlots>
{
    private readonly IEventStore _eventStore;

    public CharacterHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    /// <summary>
    /// Обрабатывает команду <see cref="CreateCharacter"/>, создавая новый агрегат персонажа.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор, имя и максимальное количество хитов нового персонажа.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    public async Task Handle(CreateCharacter command, CancellationToken cancellationToken)
    {
        var aggregate = new CharacterAggregate(command.CharacterId, command.Name, command.MaxHitPoints);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="UpdateCharacter"/>, обновляя основные данные персонажа.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа, новое имя и максимальное количество хитов.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(UpdateCharacter command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.Update(command.Name, command.MaxHitPoints);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="DealDamage"/>, нанося урон персонажу.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и количество урона.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(DealDamage command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.TakeDamage(command.Amount);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="HealCharacter"/>, восстанавливая хиты персонажу.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и количество восстанавливаемых хитов.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(HealCharacter command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.Heal(command.Amount);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="SetTemporaryHitPoints"/>, устанавливая временные хиты.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и количество временных хитов.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(SetTemporaryHitPoints command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.SetTemporaryHitPoints(command.Amount);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="GainExperience"/>, добавляя опыт персонажу.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и количество получаемого опыта.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(GainExperience command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.GainExperience(command.ExperiencePoints);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="LevelUpCharacter"/>, повышая уровень персонажа.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и новый уровень.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(LevelUpCharacter command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.LevelUp(command.NewLevel);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="SetAbilityScore"/>, устанавливая значение характеристики.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа, характеристику и её значение.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(SetAbilityScore command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.SetAbilityScore(command.Ability, command.Score);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="AddGold"/>, добавляя золото персонажу.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и количество добавляемого золота.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(AddGold command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.AddGold(command.Amount);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="SpendGold"/>, списывая золото у персонажа.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и количество тратимого золота.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(SpendGold command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.SpendGold(command.Amount);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="SetGoldCommand"/>, устанавливая точное количество золота.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и новое количество золота.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(SetGoldCommand command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.SetGold(command.Amount);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="ClearAllConditionsCommand"/>, снимая все состояния с персонажа.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(ClearAllConditionsCommand command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.ClearAllConditions();
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="AddSpell"/>, добавляя заклинание в список персонажа.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и идентификатор заклинания.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(AddSpell command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.AddSpell(command.SpellId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="RemoveSpell"/>, удаляя заклинание из списка персонажа.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и идентификатор заклинания.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(RemoveSpell command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RemoveSpell(command.SpellId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="CastSpell"/>, применяя заклинание.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа, идентификатор заклинания и уровень ячейки.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(CastSpell command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.CastSpell(command.SpellId, command.SpellSlotLevel);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="TakeShortRest"/>, выполняя короткий отдых.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и затраченные кости хитов.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(TakeShortRest command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.TakeShortRest(command.HitDiceSpent?.Count ?? 0);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="TakeLongRest"/>, выполняя продолжительный отдых.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(TakeLongRest command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.TakeLongRest();
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="ApplyCondition"/>, накладывая состояние на персонажа.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа, тип состояния и длительность в раундах.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(ApplyCondition command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.ApplyCondition(command.ConditionType, command.DurationRounds);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="RemoveCondition"/>, снимая состояние с персонажа.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и тип состояния.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(RemoveCondition command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RemoveCondition(command.ConditionType);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="MakeSavingThrow"/>, выполняя спасбросок.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа, характеристику, сложность и результат броска.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(MakeSavingThrow command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.MakeSavingThrow(command.AbilityType, command.DifficultyClass, command.RollResult);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="DeathSavingThrow"/>, выполняя спасбросок от смерти.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и результат броска.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(DeathSavingThrow command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.MakeDeathSavingThrow(command.RollResult);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="StabilizeCharacter"/>, стабилизируя персонажа.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(StabilizeCharacter command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.Stabilize();
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="UpdateTemporaryHitPoints"/>, обновляя временные хиты.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и новое количество временных хитов.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(UpdateTemporaryHitPoints command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.SetTemporaryHitPoints(command.Amount);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="UpdateArmorClass"/>, обновляя класс брони.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и новое значение класса брони.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(UpdateArmorClass command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.UpdateArmorClass(command.NewArmorClass);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="UpdateSpeed"/>, обновляя скорость персонажа.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и новое значение скорости.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(UpdateSpeed command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.UpdateSpeed(command.NewSpeed);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="UpdateProficiencyBonus"/>, обновляя бонус мастерства.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и новое значение бонуса мастерства.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(UpdateProficiencyBonus command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.SetProficiencyBonus(command.Bonus);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="AddInventoryItem"/>, добавляя предмет в инвентарь.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа, идентификатор предмета, название и количество.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(AddInventoryItem command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.AddInventoryItem(command.ItemId, command.ItemName, command.Quantity);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="RemoveInventoryItem"/>, удаляя предмет из инвентаря.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа, идентификатор предмета и количество.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(RemoveInventoryItem command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RemoveInventoryItem(command.ItemId, command.Quantity);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="EquipItem"/>, экипируя предмет.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа, идентификатор предмета, слот, название, бонус брони и бонус урона.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(EquipItem command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.EquipItem(command.ItemId, command.Slot, command.ItemName, command.ArmorBonus, command.DamageBonus);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="UnequipItem"/>, снимая предмет.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и идентификатор предмета.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(UnequipItem command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.UnequipItem(command.ItemId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="ChooseRace"/>, выбирая расу персонажа.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и идентификатор расы.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(ChooseRace command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.ChooseRace(command.RaceId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="ChooseClass"/>, выбирая класс персонажа.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и идентификатор класса.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(ChooseClass command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.ChooseClass(command.ClassId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="ChooseBackground"/>, выбирая предысторию персонажа.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и идентификатор предыстории.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(ChooseBackground command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.ChooseBackground(command.BackgroundId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="AddSkillProficiency"/>, добавляя владение навыком.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и название навыка.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(AddSkillProficiency command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.AddSkillProficiency(command.SkillName);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="RemoveSkillProficiency"/>, удаляя владение навыком.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и название навыка.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(RemoveSkillProficiency command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RemoveSkillProficiency(command.SkillName);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="AddSavingThrowProficiency"/>, добавляя владение спасброском.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и характеристику.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(AddSavingThrowProficiency command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.AddSavingThrowProficiency(command.Ability);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="RemoveSavingThrowProficiency"/>, удаляя владение спасброском.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и характеристику.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(RemoveSavingThrowProficiency command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RemoveSavingThrowProficiency(command.Ability);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="AddFeat"/>, добавляя черту персонажу.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и идентификатор черты.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(AddFeat command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.AddFeat(command.FeatId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="RemoveFeat"/>, удаляя черту у персонажа.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и идентификатор черты.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(RemoveFeat command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RemoveFeat(command.FeatId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="PrepareSpell"/>, подготавливая заклинание.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и идентификатор заклинания.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(PrepareSpell command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.PrepareSpell(command.SpellId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="UnprepareSpell"/>, отменяя подготовку заклинания.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и идентификатор заклинания.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(UnprepareSpell command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.UnprepareSpell(command.SpellId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="UseClassFeature"/>, используя классовое умение.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и идентификатор умения.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(UseClassFeature command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.UseClassFeature(command.FeatureId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="RechargeFeature"/>, восстанавливая использование умения.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и идентификатор умения.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(RechargeFeature command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RechargeFeature(command.FeatureId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="AttuneItem"/>, настраивая предмет.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и идентификатор предмета.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(AttuneItem command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.AttuneItem(command.ItemId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="UnattuneItem"/>, отменяя настройку предмета.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и идентификатор предмета.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(UnattuneItem command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.UnattuneItem(command.ItemId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="AddResistance"/>, добавляя сопротивление урону.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и тип урона.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(AddResistance command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.AddResistance(command.DamageType);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="RemoveResistance"/>, удаляя сопротивление урону.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и тип урона.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(RemoveResistance command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RemoveResistance(command.DamageType);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="AddVulnerability"/>, добавляя уязвимость к урону.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и тип урона.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(AddVulnerability command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.AddVulnerability(command.DamageType);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="RemoveVulnerability"/>, удаляя уязвимость к урону.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и тип урона.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(RemoveVulnerability command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RemoveVulnerability(command.DamageType);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="AddImmunity"/>, добавляя иммунитет к урону.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и тип урона.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(AddImmunity command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.AddImmunity(command.DamageType);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="RemoveImmunity"/>, удаляя иммунитет к урону.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и тип урона.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(RemoveImmunity command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RemoveImmunity(command.DamageType);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="ReviveCharacter"/>, воскрешая персонажа.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и количество хитов после воскрешения.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(ReviveCharacter command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.Revive(command.HitPointsAfterRevive);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="ResetDeathSavingThrows"/>, сбрасывая счётчики спасбросков от смерти.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(ResetDeathSavingThrows command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.ResetDeathSavingThrows();
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="UseSpellSlot"/>, расходуя ячейку заклинания.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа и уровень ячейки.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(UseSpellSlot command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.UseSpellSlot(command.SlotLevel);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду <see cref="RestoreAllSpellSlots"/>, восстанавливая все ячейки заклинаний.
    /// </summary>
    /// <param name="command">Команда, содержащая идентификатор персонажа.</param>
    /// <param name="cancellationToken">Токен для уведомления об отмене операции.</param>
    /// <exception cref="InvalidAction">Выбрасывается, если персонаж не найден.</exception>
    public async Task Handle(RestoreAllSpellSlots command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RestoreAllSpellSlots();
        await _eventStore.Save(aggregate, cancellationToken);
    }
}