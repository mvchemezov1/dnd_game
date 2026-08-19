// application/projections/combat_dto.cs
namespace dnd_game.Application.Projections;

/// <summary>
/// DTO состояния боя для read-модели.
/// Представляет снимок текущего состояния боевой сцены для отображения и передачи данных.
/// </summary>
/// <param name="CombatId">Идентификатор боя.</param>
/// <param name="IsActive">Активен ли бой в данный момент.</param>
/// <param name="Participants">Список участников боя с их состояниями.</param>
/// <param name="Round">Текущий раунд (начиная с 1).</param>
/// <param name="TurnIndex">Индекс текущего участника в списке <paramref name="Participants"/>.</param>
public record CombatStatusDto(
    Guid CombatId,
    bool IsActive,
    List<CombatParticipantDto> Participants,
    int Round = 1,
    int TurnIndex = 0  // индекс текущего участника в списке Participants
);

/// <summary>
/// DTO участника боя для отображения в интерфейсе.
/// Содержит информацию о текущем состоянии участника: инициатива, доступные действия,
/// оставшееся движение, активные состояния и концентрация.
/// </summary>
/// <param name="CharacterId">Идентификатор персонажа.</param>
/// <param name="Initiative">Инициатива (чем выше, тем раньше ходит).</param>
/// <param name="IsCurrentTurn">Ходит ли участник в данный момент.</param>
/// <param name="HasAction">Доступно ли основное действие.</param>
/// <param name="HasBonusAction">Доступно ли бонусное действие.</param>
/// <param name="HasReaction">Доступна ли реакция.</param>
/// <param name="HasMovement">Доступно ли движение.</param>
/// <param name="MovementRemaining">Оставшееся движение в футах.</param>
/// <param name="Conditions">Список активных состояний (например, "оглох", "ослеплён").</param>
/// <param name="Concentrating">Поддерживает ли концентрацию на заклинании.</param>
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