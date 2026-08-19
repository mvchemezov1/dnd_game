// domain/aggregates/base.cs
using dnd_game.Domain.Events;

namespace dnd_game.Domain.Aggregates
{
    /// <summary>
    /// Базовый класс для всех агрегатов, использующих событийно-ориентированное восстановление состояния.
    /// Расширен возможностями, необходимыми для реализации правил DnD (инварианты, метаданные, строгая типизация событий).
    /// </summary>
    public abstract class AggregateRoot
    {
        private readonly List<IDomainEvent> _uncommittedEvents = []; // вместо new List<IDomainEvent>()

        /// <summary>Идентификатор агрегата.</summary>
        public Guid Id { get; protected set; }

        /// <summary>Текущая версия агрегата (количество применённых событий).</summary>
        public int Version { get; protected set; }

        /// <summary>
        /// Версия агрегата, с которой он был загружен из хранилища.
        /// Используется для проверки оптимистической блокировки при сохранении.
        /// </summary>
        public int OriginalVersion { get; private set; }

        /// <summary>
        /// Установить версию агрегата (вызывается EventStore при загрузке).
        /// </summary>
        public void SetVersion(int version)
        {
            Version = version;
            OriginalVersion = version;
        }

        // --------------------------------------------------------------------------------------------
        // Применение событий
        // --------------------------------------------------------------------------------------------

        /// <summary>
        /// Применить событие к агрегату: обновляет состояние, добавляет в список несохранённых.
        /// После применения вызывает проверку инвариантов.
        /// </summary>
        public void ApplyChange(IDomainEvent @event)
        {
            ApplyEvent(@event);
            EnsureInvariants();
            _uncommittedEvents.Add(@event);
            Version++;
        }

        /// <summary>
        /// Абстрактный метод, реализующий мутацию состояния для конкретного типа события.
        /// Вызывается как при первоначальном применении, так и при восстановлении из истории.
        /// </summary>
        protected abstract void ApplyEvent(IDomainEvent @event);

        /// <summary>
        /// Проверка инвариантов агрегата (соответствие правилам DnD).
        /// Должна переопределяться в конкретных агрегатах, например:
        /// - хиты не могут быть отрицательными,
        /// - уровень персонажа не превышает 20,
        /// - количество использованных ячеек заклинаний не превышает максимума.
        /// </summary>
        public virtual void EnsureInvariants()
        {
            // Базовая реализация пустая – конкретные агрегаты добавляют свои проверки.
        }

        // --------------------------------------------------------------------------------------------
        // Восстановление состояния из истории событий
        // --------------------------------------------------------------------------------------------

        /// <summary>
        /// Восстановить состояние агрегата из списка событий (при загрузке из EventStore).
        /// После восстановления также проверяются инварианты.
        /// </summary>
        public void LoadFromHistory(IEnumerable<IDomainEvent> history)
        {
            foreach (var @event in history)
            {
                ApplyEvent(@event);
                Version++;
            }
            OriginalVersion = Version;
            EnsureInvariants(); // гарантируем, что агрегат корректен после загрузки
        }

        // --------------------------------------------------------------------------------------------
        // Работа с несохранёнными событиями
        // --------------------------------------------------------------------------------------------

        /// <summary>Получить список событий, которые ещё не были сохранены в EventStore.</summary>
        public IEnumerable<IDomainEvent> GetUncommittedEvents() => _uncommittedEvents;

        /// <summary>Очистить список несохранённых событий (вызывается после успешного сохранения).</summary>
        public void ClearUncommittedEvents()
        {
            _uncommittedEvents.Clear();
            OriginalVersion = Version; // сбрасываем для следующего цикла изменений
        }

        // --------------------------------------------------------------------------------------------
        // Вспомогательные методы для типичных сценариев D&D
        // --------------------------------------------------------------------------------------------

        /// <summary>
        /// Удобный метод для проверки, что агрегат не был изменён с момента загрузки (оптимистическая блокировка).
        /// Используется при сохранении, чтобы предотвратить конфликты.
        /// </summary>
        public bool HasConcurrencyConflict(int expectedVersion)
        {
            return OriginalVersion != expectedVersion;
        }

        /// <summary>
        /// Пометить агрегат как удалённый (например, персонаж окончательно мёртв и не может использоваться).
        /// </summary>
        public bool IsDeleted { get; private set; }

        /// <summary>
        /// Удалить агрегат (применяет событие удаления, если оно поддерживается).
        /// </summary>
        protected void MarkAsDeleted()
        {
            var @event = new AggregateDeleted(Id);
            ApplyChange(@event);
            IsDeleted = true;
        }

        // Пример вложенного события удаления (для внутреннего использования)
        public class AggregateDeleted(Guid aggregateId) : IDomainEvent
        {
            public Guid AggregateId { get; } = aggregateId;
        }
    }
}