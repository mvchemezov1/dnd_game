using dnd_game.Domain.Aggregates;
using dnd_game.Domain.Commands;
using dnd_game.Domain.Events;
using dnd_game.Domain.Exceptions;
using dnd_game.Infrastructure.EventStore;
using dnd_game.Application.CommandHandlers;

namespace dnd_game.application.command_handlers;

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

    public async Task Handle(CreateCharacter command, CancellationToken cancellationToken)
    {
        var aggregate = new CharacterAggregate(command.CharacterId, command.Name, command.MaxHitPoints);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(UpdateCharacter command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.Update(command.Name, command.MaxHitPoints);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(DealDamage command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.TakeDamage(command.Amount);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(HealCharacter command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.Heal(command.Amount);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(SetTemporaryHitPoints command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.SetTemporaryHitPoints(command.Amount);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(GainExperience command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.GainExperience(command.ExperiencePoints);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(LevelUpCharacter command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.LevelUp(command.NewLevel);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(SetAbilityScore command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.SetAbilityScore(command.Ability, command.Score);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(AddGold command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.AddGold(command.Amount);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(SpendGold command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.SpendGold(command.Amount);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(SetGoldCommand command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.SetGold(command.Amount);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(ClearAllConditionsCommand command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.ClearAllConditions();
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(AddSpell command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.AddSpell(command.SpellId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(RemoveSpell command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RemoveSpell(command.SpellId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(CastSpell command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.CastSpell(command.SpellId, command.SpellSlotLevel);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(TakeShortRest command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.TakeShortRest(command.HitDiceSpent?.Count ?? 0);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(TakeLongRest command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.TakeLongRest();
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(ApplyCondition command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.ApplyCondition(command.ConditionType, command.DurationRounds);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(RemoveCondition command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RemoveCondition(command.ConditionType);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(MakeSavingThrow command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.MakeSavingThrow(command.AbilityType, command.DifficultyClass, command.RollResult);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(DeathSavingThrow command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.MakeDeathSavingThrow(command.RollResult);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(StabilizeCharacter command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.Stabilize();
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(UpdateTemporaryHitPoints command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.SetTemporaryHitPoints(command.Amount);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(UpdateArmorClass command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.UpdateArmorClass(command.NewArmorClass);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(UpdateSpeed command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.UpdateSpeed(command.NewSpeed);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(UpdateProficiencyBonus command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.SetProficiencyBonus(command.Bonus);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(AddInventoryItem command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.AddInventoryItem(command.ItemId, command.ItemName, command.Quantity);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(RemoveInventoryItem command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RemoveInventoryItem(command.ItemId, command.Quantity);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(EquipItem command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.EquipItem(command.ItemId, command.Slot, command.ItemName, command.ArmorBonus, command.DamageBonus);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(UnequipItem command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.UnequipItem(command.ItemId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(ChooseRace command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.ChooseRace(command.RaceId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(ChooseClass command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.ChooseClass(command.ClassId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(ChooseBackground command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.ChooseBackground(command.BackgroundId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(AddSkillProficiency command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.AddSkillProficiency(command.SkillName);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(RemoveSkillProficiency command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RemoveSkillProficiency(command.SkillName);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(AddSavingThrowProficiency command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.AddSavingThrowProficiency(command.Ability);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(RemoveSavingThrowProficiency command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RemoveSavingThrowProficiency(command.Ability);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(AddFeat command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.AddFeat(command.FeatId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(RemoveFeat command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RemoveFeat(command.FeatId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(PrepareSpell command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.PrepareSpell(command.SpellId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(UnprepareSpell command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.UnprepareSpell(command.SpellId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(UseClassFeature command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.UseClassFeature(command.FeatureId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(RechargeFeature command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RechargeFeature(command.FeatureId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(AttuneItem command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.AttuneItem(command.ItemId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(UnattuneItem command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.UnattuneItem(command.ItemId);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(AddResistance command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.AddResistance(command.DamageType);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(RemoveResistance command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RemoveResistance(command.DamageType);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(AddVulnerability command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.AddVulnerability(command.DamageType);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(RemoveVulnerability command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RemoveVulnerability(command.DamageType);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(AddImmunity command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.AddImmunity(command.DamageType);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(RemoveImmunity command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RemoveImmunity(command.DamageType);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(ReviveCharacter command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.Revive(command.HitPointsAfterRevive);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(ResetDeathSavingThrows command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.ResetDeathSavingThrows();
        await _eventStore.Save(aggregate, cancellationToken);
    }
    public async Task Handle(UseSpellSlot command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.UseSpellSlot(command.SlotLevel);
        await _eventStore.Save(aggregate, cancellationToken);
    }

    public async Task Handle(RestoreAllSpellSlots command, CancellationToken cancellationToken)
    {
        var aggregate = await _eventStore.Load<CharacterAggregate>(command.CharacterId, cancellationToken)
                        ?? throw new InvalidAction("Character not found");
        aggregate.RestoreAllSpellSlots();
        await _eventStore.Save(aggregate, cancellationToken);
    }
}