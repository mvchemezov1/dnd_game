// application/projections/combat_dto.cs
namespace dnd_game.Application.Projections;

public record CombatStatusDto(
    Guid CombatId,
    bool IsActive,
    List<CombatParticipantDto> Participants,
    int Round = 1,
    int TurnIndex = 0  // индекс текущего участника в списке Participants
);

public record CombatParticipantDto(
    Guid CharacterId,
    int Initiative,
    bool IsCurrentTurn = false,
    bool HasAction = true,
    bool HasBonusAction = true,
    bool HasReaction = true,
    bool HasMovement = true,
    int MovementRemaining = 0,  // футы
    List<string> Conditions = null!,
    bool Concentrating = false
);