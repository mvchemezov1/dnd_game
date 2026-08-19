// domain/exceptions/invalid_action.cs
using dnd_game.Domain.Exceptions;

namespace dnd_game.Domain.Exceptions
{
    /// <summary>
    /// Исключение, возникающее при попытке выполнить действие,
    /// которое запрещено правилами DnD или невозможно в текущем состоянии.
    /// </summary>
    public class InvalidAction : DomainError
    {
        /// <summary>
        /// Название действия (например, "Attack", "CastSpell", "Move").
        /// </summary>
        public string ActionName { get; }

        /// <summary>
        /// Идентификатор персонажа, попытавшегося выполнить действие (если применимо).
        /// </summary>
        public Guid? CharacterId { get; }

        /// <summary>
        /// Дополнительная причина, объясняющая, почему действие недопустимо.
        /// </summary>
        public string Reason { get; }

        // ---------- Конструкторы ----------

        /// <summary>
        /// Создаёт исключение с общим сообщением (без указания действия и персонажа).
        /// </summary>
        public InvalidAction(string message)
            : base(message)
        {
            ActionName = string.Empty;
            CharacterId = null;
            Reason = message;
        }

        /// <summary>
        /// Создаёт исключение с указанием названия действия.
        /// </summary>
        public InvalidAction(string actionName, string message)
            : base($"Cannot perform action '{actionName}': {message}")
        {
            ActionName = actionName;
            CharacterId = null;
            Reason = message;
        }

        /// <summary>
        /// Создаёт исключение для конкретного персонажа и действия.
        /// </summary>
        public InvalidAction(Guid characterId, string actionName, string message)
            : base($"Character '{characterId}' cannot perform action '{actionName}': {message}")
        {
            CharacterId = characterId;
            ActionName = actionName;
            Reason = message;
        }

        /// <summary>
        /// Создаёт исключение с полным контекстом.
        /// </summary>
        public InvalidAction(Guid? characterId, string actionName, string reason, string detailedMessage)
            : base(detailedMessage)
        {
            CharacterId = characterId;
            ActionName = actionName;
            Reason = reason;
        }

        // ---------- Статические фабричные методы для типичных ситуаций ----------

        /// <summary>
        /// Персонаж мёртв и не может выполнять действия.
        /// </summary>
        public static InvalidAction CharacterIsDead(Guid characterId, string actionName)
            => new(characterId, actionName, "Character is dead.");

        /// <summary>
        /// Персонаж без сознания (0 хитов, не стабилизирован).
        /// </summary>
        public static InvalidAction CharacterIsUnconscious(Guid characterId, string actionName)
            => new(characterId, actionName, "Character is unconscious.");

        /// <summary>
        /// Персонаж ошеломлён (Stunned) и не может действовать.
        /// </summary>
        public static InvalidAction CharacterIsStunned(Guid characterId, string actionName)
            => new(characterId, actionName, "Character is stunned.");

        /// <summary>
        /// Персонаж парализован.
        /// </summary>
        public static InvalidAction CharacterIsParalyzed(Guid characterId, string actionName)
            => new(characterId, actionName, "Character is paralyzed.");

        /// <summary>
        /// Недостаточно ресурсов (например, ячеек заклинаний).
        /// </summary>
        public static InvalidAction InsufficientResource(Guid characterId, string resourceName, int required, int available)
            => new(characterId, "UseResource", $"Requires {required} {resourceName}, but only {available} available.");

        /// <summary>
        /// Действие требует концентрации, но персонаж уже концентрируется на другом заклинании.
        /// </summary>
        public static InvalidAction AlreadyConcentrating(Guid characterId, string newSpellId, string currentSpellId)
            => new(characterId, "StartConcentration", $"Already concentrating on '{currentSpellId}'. Cannot concentrate on '{newSpellId}'.");

        /// <summary>
        /// Попытка использовать два основных действия за один ход.
        /// </summary>
        public static InvalidAction NoActionAvailable(Guid characterId)
            => new(characterId, "StandardAction", "No action available this turn.");

        /// <summary>
        /// Попытка переместиться на расстояние, превышающее оставшуюся скорость.
        /// </summary>
        public static InvalidAction NotEnoughMovement(Guid characterId, int remaining, int requested)
            => new(characterId, "Move", $"Movement remaining: {remaining} ft, requested: {requested} ft.");

        /// <summary>
        /// Попытка отдыха, когда он невозможен (в бою, например).
        /// </summary>
        public static InvalidAction RestNotAllowed(Guid characterId, string reason)
            => new(characterId, "Rest", reason);

        /// <summary>
        /// Попытка изменить характеристику вне допустимых границ.
        /// </summary>
        public static InvalidAction InvalidAbilityScore(Guid characterId, string ability, int score)
            => new(characterId, "SetAbilityScore", $"Ability score for '{ability}' cannot be {score}.");
    }
}