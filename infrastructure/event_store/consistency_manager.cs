// infrastructure/event_store/consistency_manager.cs
using dnd_game.Domain.Aggregates;
using dnd_game.Domain.Exceptions;
using dnd_game.Infrastructure.Coordination; // IDistributedLockManager, LockKeyFactory, LockMode
using dnd_game.Infrastructure.EventStore;
using dnd_game.Infrastructure.Monitoring;
using Microsoft.Extensions.Logging;

namespace dnd_game.infrastructure.event_store
{
    /// <summary>
    /// Результат проверки согласованности.
    /// </summary>
    public enum ConsistencyResult
    {
        Success,
        VersionConflict,
        InvariantViolation,
        GlobalRuleViolation,
        LockTimeout
    }

    /// <summary>
    /// Менеджер согласованности, гарантирующий соблюдение правил DnD при сохранении агрегатов.
    /// Отвечает за:
    /// - оптимистическую блокировку по версии агрегата,
    /// - проверку инвариантов конкретного агрегата,
    /// - глобальные инварианты (например, уникальность концентрации у персонажа),
    /// - принудительную блокировку ресурса на время сохранения.
    /// </summary>
    public interface IConsistencyManager
    {
        /// <summary>
        /// Проверить согласованность агрегата перед сохранением и при необходимости
        /// применить пессимистическую блокировку.
        /// Возвращает результат проверки.
        /// </summary>
        /// <param name="aggregate">Агрегат с несохранёнными событиями.</param>
        /// <param name="expectedVersion">Версия, ожидаемая клиентом (для оптимистической блокировки).</param>
        /// <param name="ownerId">Идентификатор пользователя/сессии, выполняющего сохранение.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        Task<ConsistencyResult> EnforceConsistencyAsync(
            AggregateRoot aggregate,
            int expectedVersion,
            string ownerId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Проверить глобальные инварианты, не привязанные к одному агрегату
        /// (например, запрет двух заклинаний с концентрацией на одном персонаже).
        /// Должен вызываться перед сохранением после проверки версий.
        /// </summary>
        Task<bool> CheckGlobalInvariantsAsync(AggregateRoot aggregate);
    }

    /// <summary>
    /// Реализация <see cref="IConsistencyManager"/> с использованием EventStore и блокировок.
    /// </summary>
    public class ConsistencyManager(
        IServiceProvider serviceProvider,
        IDistributedLockManager lockManager,
        ILogger<ConsistencyManager> logger,
        IMetricsCollector metrics) : IConsistencyManager
    {
        private readonly Lazy<IEventStore> _eventStore = new(() => serviceProvider.GetRequiredService<IEventStore>());

        private readonly IMetricsCollector _metrics = metrics;

        public async Task<ConsistencyResult> EnforceConsistencyAsync(
            AggregateRoot aggregate,
            int expectedVersion,
            string ownerId,
            CancellationToken cancellationToken = default)
        {
            // 1. Пессимистическая блокировка для предотвращения одновременных изменений
            string lockKey = LockKeyFactory.ForAggregate(aggregate.Id);
            using var lockHandle = await lockManager.AcquireAsync(
                lockKey, LockMode.Exclusive, ownerId, TimeSpan.FromSeconds(5), cancellationToken);

            if (lockHandle == null)
            {
                logger.LogWarning("Consistency lock timeout for aggregate {AggregateId}", aggregate.Id);
                _metrics.IncrementCounter("dnd.consistency.lock_timeout");
                return ConsistencyResult.LockTimeout;
            }

            if (aggregate.OriginalVersion != expectedVersion)
            {
                logger.LogWarning(
                    "Version conflict for aggregate {AggregateId}: expected {ExpectedVersion}, actual {ActualVersion}",
                    aggregate.Id, expectedVersion, aggregate.OriginalVersion);
                _metrics.IncrementCounter("dnd.consistency.version_conflict");
                return ConsistencyResult.VersionConflict;
            }

            // 3. Проверка инвариантов самого агрегата (вызывается через его метод EnsureInvariants)
            try
            {
                aggregate.EnsureInvariants();
            }
            catch (RuleViolation ex)
            {
                logger.LogWarning("Invariant violation in aggregate {AggregateId}: {Message}", aggregate.Id, ex.Message);
                _metrics.IncrementCounter("dnd.consistency.invariant_violation");
                return ConsistencyResult.InvariantViolation;
            }

            if (!await CheckGlobalInvariantsAsync(aggregate))
            {
                _metrics.IncrementCounter("dnd.consistency.global_rule_violation");
                return ConsistencyResult.GlobalRuleViolation;
            }

            return ConsistencyResult.Success;
        }

        public async Task<bool> CheckGlobalInvariantsAsync(AggregateRoot aggregate)
        {
            // Примеры глобальных правил, основанных на правилах D&D:

            // Если агрегат — персонаж, проверяем, что у него нет двух активных концентраций
            if (aggregate is CharacterAggregate character)
            {
                if (character.Concentrating)
                {
                    // Загружаем последнее состояние персонажа из EventStore (или проекции)
                    var existing = await _eventStore.Value.Load<CharacterAggregate>(character.Id, CancellationToken.None);
                    if (existing != null && existing.Concentrating &&
                        existing.ConcentratingOnSpellId != character.ConcentratingOnSpellId)
                    {
                        logger.LogWarning("Character {CharacterId} attempted to concentrate on {NewSpell} while already concentrating on {ExistingSpell}",
                            character.Id, character.ConcentratingOnSpellId, existing.ConcentratingOnSpellId);
                        return false;
                    }
                }

                // Нельзя превысить максимальный уровень 20
                if (character.Level > 20)
                {
                    logger.LogWarning("Character {CharacterId} level {Level} exceeds maximum 20", character.Id, character.Level);
                    return false;
                }
            }

            // Можно добавить другие глобальные правила: например, уникальность магического предмета в мире и т.д.
            return true;
        }
    }

    /// <summary>
    /// Дополнительные методы для LockKeyFactory.
    /// </summary>
    public static partial class LockKeyFactory
    {
        public static string ForAggregate(Guid aggregateId) => $"Aggregate:{aggregateId}";
    }
}