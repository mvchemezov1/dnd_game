// infrastructure/event_store/snapshot_store.cs
using dnd_game.Domain.Aggregates;
using dnd_game.Domain.Events;
using Npgsql;
using NpgsqlTypes;
using System.Text;
using System.Text.Json;

namespace dnd_game.Infrastructure.EventStore
{
    /// <summary>
    /// Политика создания снимков.
    /// </summary>
    public enum SnapshotPolicy
    {
        /// <summary>Создавать снимок каждые N событий.</summary>
        EventCount,
        /// <summary>Создавать снимок через заданный интервал времени.</summary>
        TimeInterval,
        /// <summary>Не создавать снимки автоматически.</summary>
        Manual
    }

    /// <summary>
    /// Конфигурация создания снимков.
    /// </summary>
    public class SnapshotConfiguration
    {
        public SnapshotPolicy Policy { get; set; } = SnapshotPolicy.EventCount;
        public int EventCountInterval { get; set; } = 100;  // для EventCount
        public TimeSpan TimeInterval { get; set; } = TimeSpan.FromMinutes(30); // для TimeInterval
    }

    /// <summary>
    /// Интерфейс хранилища снимков.
    /// </summary>
    public interface ISnapshotStore
    {
        /// <summary>
        /// Получить последний снимок агрегата, версия которого не превышает заданную.
        /// </summary>
        Task<Snapshot?> GetLatestSnapshotAsync(Guid aggregateId, int maxVersion);

        /// <summary>
        /// Сохранить снимок агрегата.
        /// </summary>
        Task SaveSnapshotAsync(Snapshot snapshot);

        /// <summary>
        /// Удалить снимки старше указанной версии (опционально).
        /// </summary>
        Task DeleteSnapshotsOlderThanAsync(Guid aggregateId, int minVersionToKeep);

        /// <summary>
        /// Проверить, нужно ли создать новый снимок для агрегата (на основе политики).
        /// </summary>
        Task<bool> ShouldCreateSnapshotAsync(Guid aggregateId, int currentVersion);
    }

    /// <summary>
    /// Реализация хранилища снимков в памяти/файле/БД (базовая).
    /// Включает механизм сериализации агрегата и политику создания.
    /// </summary>
    // infrastructure/event_store/snapshot_store.c

    public class SnapshotStore : ISnapshotStore
    {
        private readonly string _connectionString;
        private readonly SnapshotConfiguration _config;

        public SnapshotStore(string connectionString, SnapshotConfiguration config)
        {
            _connectionString = connectionString;
            _config = config;
        }

        public async Task<Snapshot?> GetLatestSnapshotAsync(Guid aggregateId, int maxVersion)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"
            SELECT version, data, created_at
            FROM snapshots
            WHERE aggregate_id = @aggId AND version <= @maxVer
            ORDER BY version DESC LIMIT 1
        ", conn);
            cmd.Parameters.AddWithValue("aggId", NpgsqlDbType.Uuid, aggregateId);
            cmd.Parameters.AddWithValue("maxVer", maxVersion);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Snapshot
                {
                    AggregateId = aggregateId,
                    Version = reader.GetInt32(0),
                    Data = (byte[])reader[1],
                    CreatedAt = reader.GetDateTime(2)
                };
            }
            return null;
        }

        public async Task SaveSnapshotAsync(Snapshot snapshot)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"
            INSERT INTO snapshots (aggregate_id, version, data, created_at)
            VALUES (@aggId, @ver, @data, @ts)
            ON CONFLICT (aggregate_id, version) DO UPDATE SET data = @data, created_at = @ts
        ", conn);
            cmd.Parameters.AddWithValue("aggId", NpgsqlDbType.Uuid, snapshot.AggregateId);
            cmd.Parameters.AddWithValue("ver", snapshot.Version);
            cmd.Parameters.AddWithValue("data", snapshot.Data);
            cmd.Parameters.AddWithValue("ts", snapshot.CreatedAt);
            await cmd.ExecuteNonQueryAsync();
        }

        public Task DeleteSnapshotsOlderThanAsync(Guid aggregateId, int minVersionToKeep)
        {
            // Опционально: можно удалять старые снапшоты для экономии места
            return Task.CompletedTask;
        }

        public async Task<bool> ShouldCreateSnapshotAsync(Guid aggregateId, int currentVersion)
        {
            var latest = await GetLatestSnapshotAsync(aggregateId, int.MaxValue);
            int lastVersion = latest?.Version ?? 0;
            return (currentVersion - lastVersion) >= _config.EventCountInterval;
        }

        public static Snapshot CreateSnapshotFromAggregate(AggregateRoot aggregate)
        {
            var json = JsonSerializer.Serialize(aggregate, aggregate.GetType());
            var data = Encoding.UTF8.GetBytes(json);
            return new Snapshot
            {
                AggregateId = aggregate.Id,
                Version = aggregate.Version,
                Data = data,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static T? RestoreAggregateFromSnapshot<T>(Snapshot snapshot) where T : AggregateRoot, new()
        {
            var json = Encoding.UTF8.GetString(snapshot.Data);
            return JsonSerializer.Deserialize<T>(json);
        }
    }
}