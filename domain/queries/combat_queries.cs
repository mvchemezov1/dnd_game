// domain/queries/combat_queries.cs
namespace dnd_game.Domain.Queries;
using dnd_game.Application.Projections;

// ---------- Запросы ----------
public record GetCombatStatus(Guid CombatId) : IQuery<CombatStatusDto?>;
public record GetCombatParticipants(Guid CombatId) : IQuery<List<CombatParticipantDto>>;
public record GetCurrentCombatParticipant(Guid CombatId) : IQuery<CombatParticipantDto?>;
public record GetCombatRound(Guid CombatId) : IQuery<int>;
public record GetCombatTurnOrder(Guid CombatId) : IQuery<List<Guid>>;
public record IsCombatActive(Guid CombatId) : IQuery<bool>;