// shared_kernel/primitives.cs
using System.Reflection;

namespace dnd_game.SharedKernel
{
    // --------------------------------------------------------------------------------
    // ValueObject (уже присутствовал)
    // --------------------------------------------------------------------------------
    public abstract class ValueObject
    {
        protected abstract IEnumerable<object> GetEqualityComponents();

        public override bool Equals(object? obj) =>
            obj is ValueObject other && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

        public override int GetHashCode() =>
            GetEqualityComponents().Aggregate(0, (hash, component) => HashCode.Combine(hash, component));
    }

    // --------------------------------------------------------------------------------
    // Ѕазова€ сущность с идентификатором
    // --------------------------------------------------------------------------------
    public abstract class Entity<TId>
    {
        public TId Id { get; protected set; } = default!;

        protected Entity() { }                       // дл€ ORM / десериализации
        protected Entity(TId id) => Id = id;

        public override bool Equals(object? obj)
        {
            if (obj is not Entity<TId> other) return false;
            if (ReferenceEquals(this, other)) return true;
            if (GetType() != other.GetType()) return false;
            return EqualityComparer<TId>.Default.Equals(Id, other.Id);
        }

        public override int GetHashCode() => Id?.GetHashCode() ?? 0;

        public static bool operator ==(Entity<TId>? left, Entity<TId>? right) => left?.Equals(right) ?? right is null;
        public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);
    }

    // --------------------------------------------------------------------------------
    // јгрегат (корень)
    // --------------------------------------------------------------------------------
    public abstract class AggregateRoot<TId> : Entity<TId>
    {
        protected AggregateRoot() { }
        protected AggregateRoot(TId id) : base(id) { }
    }

    // --------------------------------------------------------------------------------
    // ћаркерные интерфейсы доменных сообщений
    // --------------------------------------------------------------------------------
    public interface IDomainEvent { }
    public interface ICommand { }
    public interface IQuery<TResult> { }

    // --------------------------------------------------------------------------------
    // Enumeration Ц типобезопасна€ замена enum (по ‘аулеру)
    // --------------------------------------------------------------------------------
    public abstract class Enumeration : IComparable
    {
        public string Name { get; }
        public int Id { get; }

        protected Enumeration(int id, string name) => (Id, Name) = (id, name);

        public override string ToString() => Name;

        public static IEnumerable<T> GetAll<T>() where T : Enumeration =>
            typeof(T)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(f => f.GetValue(null))
                .Cast<T>();

        public override bool Equals(object? obj)
        {
            if (obj is not Enumeration other) return false;
            return GetType() == obj.GetType() && Id == other.Id;
        }

        public override int GetHashCode() => Id.GetHashCode();
        public int CompareTo(object? other) => Id.CompareTo(((Enumeration)other!).Id);
    }

    // --------------------------------------------------------------------------------
    // Result Ц монада дл€ возврата успеха / ошибки
    // --------------------------------------------------------------------------------
    public class Result<T>
    {
        public bool IsSuccess { get; }
        public T? Value { get; }
        public string? Error { get; }

        protected internal Result(bool isSuccess, T? value, string? error)
        {
            IsSuccess = isSuccess;
            Value = value;
            Error = error;
        }

        public static Result<T> Success(T value) => new(true, value, null);
        public static Result<T> Failure(string error) => new(false, default, error);

        public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, TResult> onFailure) =>
            IsSuccess ? onSuccess(Value!) : onFailure(Error!);
    }

    // --------------------------------------------------------------------------------
    // Maybe Ц опциональное значение (аналог Nullable дл€ ссылочных типов)
    // --------------------------------------------------------------------------------
    public struct Maybe<T>
    {
        public T? Value { get; }
        public bool HasValue { get; }

        internal Maybe(T? value)
        {
            Value = value;
            HasValue = value is not null;
        }

        public static Maybe<T> Some(T value) => new(value);
        public static Maybe<T> None() => new(default);
    }

    // --------------------------------------------------------------------------------
    // ќбобщЄнный интерфейс репозитори€ агрегата
    // --------------------------------------------------------------------------------
    public interface IRepository<T, in TId> where T : AggregateRoot<TId>
    {
        Task<T?> GetByIdAsync(TId id);
        Task SaveAsync(T aggregate);
        Task DeleteAsync(TId id);
    }
}