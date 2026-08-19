// shared_kernel/exceptions.cs
namespace dnd_game.SharedKernel
{
    /// <summary>
    /// Базовое исключение для всех игровых ошибок.
    /// </summary>
    public class GameException : Exception
    {
        public GameException(string message) : base(message) { }
        public GameException(string message, Exception innerException) : base(message, innerException) { }
    }

    // --------------------------------------------------------------------------------
    // Исключения валидации
    // --------------------------------------------------------------------------------
    /// <summary>
    /// Ошибка валидации входных данных (команды, запроса).
    /// </summary>
    public class ValidationException : GameException
    {
        public ValidationException(string message) : base(message) { }
    }

    /// <summary>
    /// Ошибка нарушения бизнес-правила (доменное исключение).
    /// </summary>
    public class RuleViolationException : GameException
    {
        public string RuleName { get; }

        public RuleViolationException(string ruleName, string message)
            : base($"Rule '{ruleName}': {message}")
        {
            RuleName = ruleName;
        }
    }

    // --------------------------------------------------------------------------------
    // Исключения, связанные с сущностями
    // --------------------------------------------------------------------------------
    /// <summary>
    /// Сущность не найдена.
    /// </summary>
    public class NotFoundException : GameException
    {
        public string EntityType { get; }
        public Guid EntityId { get; }

        public NotFoundException(string entityType, Guid entityId)
            : base($"{entityType} with id '{entityId}' not found.")
        {
            EntityType = entityType;
            EntityId = entityId;
        }
    }

    // --------------------------------------------------------------------------------
    // Конфликты и конкурентный доступ
    // --------------------------------------------------------------------------------
    /// <summary>
    /// Конфликт версий при оптимистической блокировке.
    /// </summary>
    public class ConcurrencyException : GameException
    {
        public Guid AggregateId { get; }
        public int ExpectedVersion { get; }
        public int ActualVersion { get; }

        public ConcurrencyException(Guid aggregateId, int expectedVersion, int actualVersion)
            : base($"Concurrency conflict for aggregate {aggregateId}: expected version {expectedVersion}, actual {actualVersion}.")
        {
            AggregateId = aggregateId;
            ExpectedVersion = expectedVersion;
            ActualVersion = actualVersion;
        }
    }

    // --------------------------------------------------------------------------------
    // Безопасность и авторизация
    // --------------------------------------------------------------------------------
    /// <summary>
    /// Недостаточно прав для выполнения действия.
    /// </summary>
    public class UnauthorizedException : GameException
    {
        public Guid UserId { get; }
        public string RequiredPermission { get; }

        public UnauthorizedException(Guid userId, string requiredPermission)
            : base($"User '{userId}' lacks permission '{requiredPermission}'.")
        {
            UserId = userId;
            RequiredPermission = requiredPermission;
        }
    }

    // --------------------------------------------------------------------------------
    // Ресурсы и лимиты
    // --------------------------------------------------------------------------------
    /// <summary>
    /// Недостаточно ресурсов (золота, предметов, ячеек и т.д.).
    /// </summary>
    public class InsufficientResourcesException : GameException
    {
        public string ResourceType { get; }
        public int Required { get; }
        public int Available { get; }

        public InsufficientResourcesException(string resourceType, int required, int available)
            : base($"Not enough {resourceType}. Required: {required}, available: {available}.")
        {
            ResourceType = resourceType;
            Required = required;
            Available = available;
        }
    }

    /// <summary>
    /// Достигнут лимит (например, максимальный уровень, количество аттунементов).
    /// </summary>
    public class LimitExceededException : GameException
    {
        public string LimitType { get; }
        public int Limit { get; }
        public int Current { get; }

        public LimitExceededException(string limitType, int limit, int current)
            : base($"Limit '{limitType}' exceeded. Maximum: {limit}, current: {current}.")
        {
            LimitType = limitType;
            Limit = limit;
            Current = current;
        }
    }

    // --------------------------------------------------------------------------------
    // Ошибки, связанные с состоянием сущности
    // --------------------------------------------------------------------------------
    /// <summary>
    /// Операция недопустима в текущем состоянии сущности.
    /// </summary>
    public class InvalidOperationForStateException : GameException
    {
        public string EntityType { get; }
        public Guid EntityId { get; }
        public string CurrentState { get; }

        public InvalidOperationForStateException(string entityType, Guid entityId, string currentState, string message)
            : base($"Cannot perform operation on {entityType} '{entityId}' in state '{currentState}': {message}")
        {
            EntityType = entityType;
            EntityId = entityId;
            CurrentState = currentState;
        }
    }
}