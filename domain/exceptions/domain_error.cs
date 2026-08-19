// domain/exceptions/domain_error.cs
namespace dnd_game.Domain.Exceptions
{
    // --------------------------------------------------------------------------------------------
    // Базовый класс (оставлен)
    // --------------------------------------------------------------------------------------------
    public class DomainError(string message) : Exception(message)
    {
    }

    // --------------------------------------------------------------------------------------------
    // Сущность не найдена (Entity Not Found)
    // --------------------------------------------------------------------------------------------
    public class EntityNotFoundException(string entityType, Guid entityId) : DomainError($"{entityType} with id '{entityId}' not found.")
    {
        public string EntityType { get; } = entityType;
        public Guid EntityId { get; } = entityId;
    }

    // --------------------------------------------------------------------------------------------
    // Конфликт состояния (State Conflict) – агрегат изменился с момента загрузки
    // --------------------------------------------------------------------------------------------
    public class StateConflictException(Guid aggregateId, int expectedVersion, int actualVersion) : DomainError($"State conflict for aggregate {aggregateId}: expected version {expectedVersion}, but found {actualVersion}.")
    {
        public Guid AggregateId { get; } = aggregateId;
        public int ExpectedVersion { get; } = expectedVersion;
        public int ActualVersion { get; } = actualVersion;
    }

    // --------------------------------------------------------------------------------------------
    // Недостаточно ресурсов (Insufficient Resources) – не хватает золота, предметов и т.д.
    // --------------------------------------------------------------------------------------------
    public class InsufficientResourcesException(Guid characterId, string resourceType, int required, int available) : DomainError($"Character '{characterId}' needs {required} {resourceType} but has only {available}.")
    {
        public Guid CharacterId { get; } = characterId;
        public string ResourceType { get; } = resourceType;
        public int Required { get; } = required;
        public int Available { get; } = available;
    }

    // --------------------------------------------------------------------------------------------
    // Ошибка боя (Combat Exception)
    // --------------------------------------------------------------------------------------------
    public class CombatException(Guid combatId, string message) : DomainError($"Combat '{combatId}': {message}")
    {
        public Guid CombatId { get; } = combatId;
    }

    // --------------------------------------------------------------------------------------------
    // Ошибка заклинания (Spell Failure)
    // --------------------------------------------------------------------------------------------
    public class SpellFailureException(Guid casterId, string spellId, string message) : DomainError($"Spell failure for '{casterId}' casting '{spellId}': {message}")
    {
        public Guid CasterId { get; } = casterId;
        public string SpellId { get; } = spellId;
    }

    // --------------------------------------------------------------------------------------------
    // Ошибка передвижения (Movement Exception)
    // --------------------------------------------------------------------------------------------
    public class MovementException(Guid characterId, string message) : DomainError($"Movement error for character '{characterId}': {message}")
    {
        public Guid CharacterId { get; } = characterId;
    }

    // --------------------------------------------------------------------------------------------
    // Ошибка отдыха (Rest Exception)
    // --------------------------------------------------------------------------------------------
    public class RestException(Guid characterId, string message) : DomainError($"Rest error for character '{characterId}': {message}")
    {
        public Guid CharacterId { get; } = characterId;
    }

    // --------------------------------------------------------------------------------------------
    // Ошибка квеста (Quest Exception)
    // --------------------------------------------------------------------------------------------
    public class QuestException(Guid questId, string message) : DomainError($"Quest '{questId}': {message}")
    {
        public Guid QuestId { get; } = questId;
    }

    // --------------------------------------------------------------------------------------------
    // Неавторизованное действие (Unauthorized) – можно перенести из Security, но часто требуется в домене
    // --------------------------------------------------------------------------------------------
    public class UnauthorizedActionException(Guid userId, string action) : DomainError($"User '{userId}' is not authorized to perform '{action}'.")
    {
        public Guid UserId { get; } = userId;
        public string Action { get; } = action;
    }
}