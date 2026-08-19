// domain/queries/base.cs
namespace dnd_game.Domain.Queries;

// --------------------------------------------------------------------------------------------
// Базовые интерфейсы
// --------------------------------------------------------------------------------------------

/// <summary>Запрос, возвращающий результат указанного типа.</summary>
public interface IQuery<TResult> { }

/// <summary>Обработчик запросов (CQRS Query Handler).</summary>
public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> Handle(TQuery query, CancellationToken cancellationToken = default);
}

// --------------------------------------------------------------------------------------------
// Контекст выполнения (игровая сессия, пользователь)
// --------------------------------------------------------------------------------------------

/// <summary>
/// Запрос, несущий контекст пользователя и игровой сессии.
/// Позволяет автоматически проверять права доступа и логировать запросы.
/// </summary>
public interface IGameQuery<TResult> : IQuery<TResult>
{
    /// <summary>Идентификатор пользователя, выполняющего запрос.</summary>
    Guid UserId { get; init; }

    /// <summary>Идентификатор активной игровой сессии (кампании).</summary>
    Guid GameSessionId { get; init; }
}

// --------------------------------------------------------------------------------------------
// Авторизация
// --------------------------------------------------------------------------------------------

/// <summary>
/// Запрос, требующий определённого разрешения для выполнения.
/// </summary>
public interface IAuthorizedQuery<TResult> : IGameQuery<TResult>
{
    /// <summary>Требуемое разрешение (например, "ViewCharacter", "EditCampaign").</summary>
    string RequiredPermission { get; }
}

// --------------------------------------------------------------------------------------------
// Пагинация
// --------------------------------------------------------------------------------------------

/// <summary>
/// Запрос с поддержкой постраничного вывода.
/// </summary>
public interface IPagedQuery<TResult> : IQuery<TResult>
{
    int PageNumber { get; init; }
    int PageSize { get; init; }
}

/// <summary>
/// Ответ с информацией о пагинации.
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}

// --------------------------------------------------------------------------------------------
// Сортировка
// --------------------------------------------------------------------------------------------

/// <summary>
/// Запрос с поддержкой сортировки.
/// </summary>
public interface ISortedQuery<TResult> : IQuery<TResult>
{
    string SortBy { get; init; }
    bool SortDescending { get; init; }
}

// --------------------------------------------------------------------------------------------
// Фильтрация
// --------------------------------------------------------------------------------------------

/// <summary>
/// Запрос с поддержкой фильтрации по ключу-значению.
/// Конкретные запросы могут раскрывать фильтры своими свойствами.
/// </summary>
public interface IFilteredQuery<TResult> : IQuery<TResult>
{
    Dictionary<string, string> Filters { get; init; }
}

// --------------------------------------------------------------------------------------------
// Базовый абстрактный класс запроса
// --------------------------------------------------------------------------------------------

/// <summary>
/// Удобная база для всех запросов с предустановленными свойствами контекста.
/// </summary>
public abstract record BaseQuery<TResult> : IGameQuery<TResult>, IPagedQuery<TResult>, ISortedQuery<TResult>
{
    public Guid UserId { get; init; }
    public Guid GameSessionId { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string SortBy { get; init; } = string.Empty;
    public bool SortDescending { get; init; }
}