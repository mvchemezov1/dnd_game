// application/projections/materialized_views/combat_status.cs
namespace dnd_game.application.projections.materialized_views;

/// <summary>
/// Материализованное представление полного состояния боя для отображения в пользовательском интерфейсе.
/// Строится на основе событий боевой сцены и предназначено исключительно для чтения.
/// </summary>
/// <remarks>
/// Данный класс является частью read-модели и не содержит бизнес-логики.
/// Обновляется проектором, который подписывается на события <c>CombatStarted</c>, <c>CombatEnded</c>,
/// <c>RoundStarted</c>, <c>TurnChanged</c> и другие, связанные с ходом боя.
/// </remarks>
public class CombatStatusView
{
    /// <summary>Идентификатор боя (агрегата <c>CombatAggregate</c>).</summary>
    public Guid CombatId { get; set; }

    /// <summary>Признак того, что бой активен в данный момент.</summary>
    public bool Active { get; set; }

    /// <summary>Текущий раунд (начиная с 1).</summary>
    public int Round { get; set; }

    /// <summary>Индекс активного участника в списке <see cref="Participants"/>.</summary>
    public int CurrentTurnIndex { get; set; }

    /// <summary>Имя персонажа, который сейчас ходит, либо пустая строка, если ход ничей.</summary>
    public string CurrentTurnCharacterName { get; set; } = string.Empty;

    /// <summary>Список всех участников боя с подробной информацией, отсортированный по инициативе (по убыванию).</summary>
    public List<CombatParticipantView> Participants { get; set; } = [];
}

/// <summary>
/// Представление участника боя для отображения в интерфейсе.
/// Содержит снимок состояния персонажа, необходимый для визуализации боевой сцены.
/// </summary>
/// <remarks>
/// Обновляется на основе событий агрегата <c>CombatAggregate</c> и <c>CharacterAggregate</c>.
/// Значения полей предназначены только для чтения и не должны изменяться напрямую.
/// </remarks>
public class CombatParticipantView
{
    /// <summary>Идентификатор персонажа, участвующего в бою.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>Имя персонажа.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Инициатива персонажа (чем выше, тем раньше ходит).</summary>
    public int Initiative { get; set; }

    /// <summary>Текущее количество хитов персонажа.</summary>
    public int CurrentHitPoints { get; set; }

    /// <summary>Максимальное количество хитов персонажа.</summary>
    public int MaxHitPoints { get; set; }

    /// <summary>Количество временных хитов.</summary>
    public int TemporaryHitPoints { get; set; }

    /// <summary>Класс брони (AC) персонажа.</summary>
    public int ArmorClass { get; set; }

    /// <summary>Оставшаяся скорость передвижения в текущем ходу (в футах).</summary>
    public int MovementRemaining { get; set; }

    /// <summary>Доступно ли основное действие в текущем ходу.</summary>
    public bool HasAction { get; set; }

    /// <summary>Доступно ли бонусное действие в текущем ходу.</summary>
    public bool HasBonusAction { get; set; }

    /// <summary>Доступна ли реакция в текущем раунде.</summary>
    public bool HasReaction { get; set; }

    /// <summary>Список активных состояний (например, "оглох", "ослеплён", "парализован").</summary>
    public List<string> Conditions { get; set; } = [];

    /// <summary>Поддерживает ли персонаж концентрацию на заклинании.</summary>
    public bool Concentrating { get; set; }

    /// <summary>Текущий статус жизни/смерти персонажа.</summary>
    public DeathStatus DeathStatus { get; set; }

    /// <summary>Количество успешных спасбросков от смерти (от 0 до 3).</summary>
    public int DeathSaveSuccesses { get; set; }

    /// <summary>Количество проваленных спасбросков от смерти (от 0 до 3).</summary>
    public int DeathSaveFailures { get; set; }
}

/// <summary>
/// Перечисление возможных состояний жизни и смерти персонажа.
/// Используется в представлениях боевого статуса для отображения текущего состояния участника.
/// </summary>
public enum DeathStatus
{
    /// <summary>Персонаж жив и дееспособен.</summary>
    Alive,

    /// <summary>Персонаж находится при смерти (0 хитов, но ещё не стабилизирован).</summary>
    Dying,

    /// <summary>Персонаж стабилизирован, но всё ещё без сознания.</summary>
    Stable,

    /// <summary>Персонаж мёртв.</summary>
    Dead
}