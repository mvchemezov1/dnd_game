// domain/commands/rest_commands.cs
namespace dnd_game.Domain.Commands;

/// <summary>
/// Начать отдых. Тип отдыха передаётся строкой: "Short" или "Long".
/// </summary>
public record StartRest(Guid CharacterId, string RestType) : ICommand;

/// <summary>
/// Потратить одну кость хитов во время короткого отдыха.
/// </summary>
/// <param name="CharacterId"></param>
/// <param name="HitDieType">Тип кости (d6, d8, d10, d12).</param>
/// <param name="Roll">Результат броска кости хитов.</param>
/// <param name="ConstitutionModifier">Модификатор телосложения персонажа.</param>
public record SpendHitDie(Guid CharacterId, int HitDieType, int Roll, int ConstitutionModifier) : ICommand;

/// <summary>
/// Прервать текущий отдых (например, из-за нападения).
/// </summary>
/// <param name="CharacterId"></param>
/// <param name="InterruptionType">Тип прерывания: "Combat", "StrenuousActivity", "Environmental".</param>
public record InterruptRest(Guid CharacterId, string InterruptionType) : ICommand;

/// <summary>
/// Завершить отдых и применить все эффекты (восстановление хитов, ячеек, снятие усталости и т.д.).
/// </summary>
public record EndRest(Guid CharacterId) : ICommand;