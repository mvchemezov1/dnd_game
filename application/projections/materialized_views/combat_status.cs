// application/projections/materialized_views/combat_status.cs
namespace dnd_game.Application.Projections.MaterializedViews;

/// <summary>
/// Полное состояние боя для отображения в интерфейсе.
/// </summary>
public class CombatStatusView
{
    /// <summary>Идентификатор боя.</summary>
    public Guid CombatId { get; set; }

    /// <summary>Активен ли бой в данный момент.</summary>
    public bool Active { get; set; }

    /// <summary>Текущий раунд (начиная с 1).</summary>
    public int Round { get; set; }

    /// <summary>Индекс активного участника в списке Participants.</summary>
    public int CurrentTurnIndex { get; set; }

    /// <summary>Имя персонажа, чей сейчас ход, либо "Нет" если никто.</summary>
    public string CurrentTurnCharacterName { get; set; } = string.Empty;

    /// <summary>Список всех участников боя с подробной информацией.</summary>
    public List<CombatParticipantView> Participants { get; set; } = new();
}

/// <summary>
/// Представление участника боя для UI.
/// </summary>
public class CombatParticipantView
{
    /// <summary>Идентификатор персонажа.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>Имя персонажа.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Инициатива (чем выше, тем раньше ходит).</summary>
    public int Initiative { get; set; }

    /// <summary>Текущие хиты.</summary>
    public int CurrentHitPoints { get; set; }

    /// <summary>Максимальные хиты.</summary>
    public int MaxHitPoints { get; set; }

    /// <summary>Временные хиты.</summary>
    public int TemporaryHitPoints { get; set; }

    /// <summary>Класс брони.</summary>
    public int ArmorClass { get; set; }

    /// <summary>Оставшаяся скорость передвижения в футах.</summary>
    public int MovementRemaining { get; set; }

    /// <summary>Есть ли доступное основное действие.</summary>
    public bool HasAction { get; set; }

    /// <summary>Есть ли доступное бонусное действие.</summary>
    public bool HasBonusAction { get; set; }

    /// <summary>Есть ли доступная реакция.</summary>
    public bool HasReaction { get; set; }

    /// <summary>Активные состояния (оглох, ослеплён и т.д.).</summary>
    public List<string> Conditions { get; set; } = new();

    /// <summary>Поддерживает ли концентрацию на заклинании.</summary>
    public bool Concentrating { get; set; }

    /// <summary>Статус смерти: жив, при смерти, стабилен, мёртв.</summary>
    public DeathStatus DeathStatus { get; set; }

    /// <summary>Успешные спасброски от смерти.</summary>
    public int DeathSaveSuccesses { get; set; }

    /// <summary>Проваленные спасброски от смерти.</summary>
    public int DeathSaveFailures { get; set; }
}

/// <summary>
/// Перечисление возможных состояний жизни/смерти.
/// </summary>
public enum DeathStatus
{
    Alive,
    Dying,
    Stable,
    Dead
}