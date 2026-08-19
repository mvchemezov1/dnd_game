// domain/events/magic_events.cs
namespace dnd_game.Domain.Events;

// ---------- Базовые (оставлены) ----------
public record SpellCast(Guid CasterId, Guid SpellId, Guid? TargetId, DateTime OccurredOn) : IDomainEvent;
public record MagicEffectApplied(Guid TargetId, string Effect, int Duration, DateTime OccurredOn) : IDomainEvent;

// ---------- Подготовка и известные заклинания ----------
public record SpellLearned(Guid CasterId, string SpellId) : IDomainEvent;
public record SpellForgotten(Guid CasterId, string SpellId) : IDomainEvent;

// ---------- Ячейки заклинаний ----------
public record SpellSlotConsumed(Guid CasterId, int SlotLevel) : IDomainEvent;

// ---------- Концентрация ----------
public record ConcentrationCheckMade(Guid CasterId, string SpellId, int DC, int RollResult, bool Success) : IDomainEvent;

// ---------- Ритуалы ----------
public record RitualCastStarted(Guid CasterId, string SpellId, int CastingTimeMinutes) : IDomainEvent;
public record RitualCastCompleted(Guid CasterId, string SpellId) : IDomainEvent;

// ---------- Свитки и магические предметы ----------
public record ScrollUsed(Guid UserId, string ScrollItemId, string SpellId) : IDomainEvent;
public record MagicItemActivated(Guid UserId, string ItemId, string EffectDescription) : IDomainEvent;
public record WandChargeUsed(Guid UserId, string ItemId, int RemainingCharges) : IDomainEvent;

// ---------- Диспелл и контрзаклинания ----------
public record SpellDispelled(Guid CasterId, Guid TargetSpellId, string DispellerId) : IDomainEvent;
public record CounterSpellAttempted(Guid CasterId, Guid OriginalCasterId, string OriginalSpellId, int SlotLevelUsed) : IDomainEvent;
public record CounterSpellResolved(Guid CasterId, string OriginalSpellId, bool Successful) : IDomainEvent;

// ---------- Урон и исцеление от заклинаний ----------
public record SpellDamageDealt(Guid CasterId, string SpellId, Guid TargetId, int DamageAmount, string DamageType) : IDomainEvent;
public record SpellHealingDealt(Guid CasterId, string SpellId, Guid TargetId, int HealingAmount) : IDomainEvent;

// ---------- Спасброски от заклинаний ----------
public record SpellSavingThrowAttempted(Guid TargetId, string SpellId, string Ability, int DC, int RollResult, bool Success) : IDomainEvent;

// ---------- Области воздействия и множественные цели ----------
public record AreaOfEffectSpellCast(Guid CasterId, string SpellId, List<Guid> AffectedTargets) : IDomainEvent;