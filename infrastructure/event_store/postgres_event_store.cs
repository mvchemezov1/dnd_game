// infrastructure/event_store/postgres_event_store.cs
using dnd_game.Domain.Aggregates;
using dnd_game.Domain.Events;
using dnd_game.Domain.Exceptions;
using dnd_game.Infrastructure.EventStore;
using dnd_game.Infrastructure.MessageBus;
using dnd_game.Infrastructure.Monitoring;
using Npgsql;
using NpgsqlTypes;
using System.Text.Json;

namespace dnd_game.infrastructure.event_store;

public class PostgresEventStore : IEventStore
{
    private readonly string _connectionString;
    private readonly ISnapshotStore _snapshotStore;
    private readonly IConsistencyManager _consistencyManager;
    private readonly ILogger<PostgresEventStore> _logger;
    private readonly IMetricsCollector _metrics;
    private readonly IEventBus _eventBus;

    public PostgresEventStore(string connectionString, ISnapshotStore snapshotStore, IConsistencyManager consistencyManager, ILogger<PostgresEventStore> logger, IMetricsCollector metrics, IEventBus eventBus)
    {
        _connectionString = connectionString;
        _snapshotStore = snapshotStore;
        _consistencyManager = consistencyManager;
        _logger = logger;
        _metrics = metrics;
        _eventBus = eventBus;
        //InitializeDatabase();
    }

    /*private void InitializeDatabase()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS events (
                id BIGSERIAL PRIMARY KEY,
                event_id UUID NOT NULL UNIQUE,
                aggregate_id UUID NOT NULL,
                aggregate_type TEXT NOT NULL,
                version INT NOT NULL,
                event_type TEXT NOT NULL,
                data JSONB NOT NULL,
                user_id UUID NOT NULL,
                session_id UUID NOT NULL,
                custom_headers JSONB,
                timestamp TIMESTAMPTZ NOT NULL DEFAULT now(),
                UNIQUE (aggregate_id, version)
            );

            CREATE TABLE IF NOT EXISTS snapshots (
                aggregate_id UUID NOT NULL,
                version INT NOT NULL,
                data BYTEA NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                PRIMARY KEY (aggregate_id, version)
            );

            -- �������� � ���������� ������� session_id, ���� ��� ���
                    DO $$
                    BEGIN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                       WHERE table_name='events' AND column_name='session_id') THEN
                            ALTER TABLE events ADD COLUMN session_id UUID;
                        END IF;
                    END $$;

            CREATE INDEX IF NOT EXISTS idx_events_aggregate_id ON events(aggregate_id);
            CREATE INDEX IF NOT EXISTS idx_events_type ON events(event_type);
            CREATE INDEX IF NOT EXISTS idx_events_session ON events(session_id);
            CREATE INDEX IF NOT EXISTS idx_events_timestamp ON events(timestamp);
        ", conn);
        cmd.ExecuteNonQuery();
    }*/

    // ---------- ���������� (������� � � �����������) ----------

    public async Task Save<T>(T aggregate) where T : AggregateRoot, new()
    {
        // ��������� ��������� ���������� (UserId � GameSessionId � ��������)
        var metadata = new EventMetadata
        {
            UserId = Guid.Empty,    // ������ �������������� �� ���������
            GameSessionId = Guid.Empty
        };
        await SaveWithMetadata(aggregate, metadata);
    }

    // infrastructure/event_store/postgres_event_store.cs
    // ���������� � �������������� ConsistencyManager
    public async Task SaveWithMetadata<T>(T aggregate, EventMetadata metadataTemplate) where T : AggregateRoot, new()
    {
        const int maxRetries = 3;
        int attempt = 0;
        while (true)
        {
            try
            {
                // ��������� ��������������� ����� ConsistencyManager
                var consistencyResult = await _consistencyManager.EnforceConsistencyAsync(
                    aggregate,
                    aggregate.OriginalVersion,
                    metadataTemplate.UserId.ToString());

                if (consistencyResult != ConsistencyResult.Success)
                {
                    throw consistencyResult switch
                    {
                        ConsistencyResult.VersionConflict => new StateConflictException(aggregate.Id, aggregate.OriginalVersion, aggregate.Version),
                        ConsistencyResult.LockTimeout => new InvalidOperationException("Lock timeout"),
                        ConsistencyResult.InvariantViolation => new RuleViolation("Invariant", "Aggregate invariants violated"),
                        ConsistencyResult.GlobalRuleViolation => new RuleViolation("Global", "Global rule violated"),
                        _ => new InvalidOperationException("Consistency check failed")
                    };
                }

                // ��������� �������
                await SaveInternal(aggregate, metadataTemplate);
                return; // �����
            }
            catch (StateConflictException) when (attempt < maxRetries)
            {
                attempt++;
                await Task.Delay(100 * (int)Math.Pow(2, attempt - 1));
                var reloaded = await Load<T>(aggregate.Id);
                if (reloaded == null)
                    throw new InvalidOperationException($"Aggregate {aggregate.Id} not found during retry.");

                var uncommitted = aggregate.GetUncommittedEvents().ToList();
                foreach (var @event in uncommitted)
                    reloaded.ApplyChange(@event);
                aggregate = reloaded;
            }
            catch (Exception ex) when (attempt >= maxRetries)
            {
                throw new InvalidOperationException($"Failed to save aggregate {aggregate.Id} after {maxRetries} retries.", ex);
            }
        }
    }

    // ---------- �������� ----------

    // �������� �������� � �������������� EventStream � ���������
    public async Task<T?> Load<T>(Guid aggregateId) where T : AggregateRoot, new()
    {
        var snapshot = await _snapshotStore.GetLatestSnapshotAsync(aggregateId, int.MaxValue);
        T? aggregate = null;

        if (snapshot != null)
        {
            aggregate = SnapshotStore.RestoreAggregateFromSnapshot<T>(snapshot);
        }

        // ��������� ������� ����� ������ �������� (��� � 0)
        int fromVersion = snapshot?.Version ?? 0;
        var stream = await GetEventStreamAsync(aggregateId, new ReadStreamOptions { FromVersion = fromVersion });
        var domainEvents = stream?.Events.Select(e => e.DomainEvent) ?? Enumerable.Empty<IDomainEvent>();

        if (aggregate == null)
        {
            aggregate = new T();
            aggregate.LoadFromHistory(domainEvents);
        }
        else
        {
            // ��������� ������� � ���������������� ��������
            foreach (var ev in domainEvents)
            {
                aggregate.ApplyChange(ev);
            }
        }

        // ������������� ������ (��� ��� ��������� ��� ApplyChange)
        // �� ����� ���������������� � OriginalVersion ��� ����������
        aggregate.SetVersion(aggregate.Version);
        return aggregate;
    }

    public async Task<T?> LoadWithMetadata<T>(Guid aggregateId) where T : AggregateRoot, new()
    {
        return await Load<T>(aggregateId); // ���������� ����� ������������ ��������
    }

    async Task<IEnumerable<StoredEvent>> IEventStore.GetEventStreamAsync(Guid aggregateId, ReadStreamOptions? options)
    {
        var stream = await GetEventStreamAsync(aggregateId, options);
        return stream?.Events ?? Enumerable.Empty<StoredEvent>();
    }

    public async Task<EventStream?> GetEventStreamAsync(Guid aggregateId, ReadStreamOptions? options = null)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        string whereClause = "WHERE aggregate_id = @aggId";
        if (options?.FromVersion > 0)
            whereClause += " AND version > @fromVer";
        if (options?.EventTypeFilter != null)
            whereClause += " AND event_type = @typeFilter";
        if (options?.FromTimestamp != null)
            whereClause += " AND timestamp >= @fromTs";

        string orderBy = options?.ReadBackwards == true ? "ORDER BY version DESC" : "ORDER BY version ASC";
        string limit = options?.MaxCount > 0 ? $"LIMIT {options.MaxCount.Value}" : "";

        using var cmd = new NpgsqlCommand($@"
            SELECT event_id, event_type, aggregate_id, aggregate_type, version, data,
                   user_id, session_id, custom_headers, timestamp
            FROM events
            {whereClause}
            {orderBy}
            {limit}
        ", conn);

        cmd.Parameters.AddWithValue("aggId", NpgsqlDbType.Uuid, aggregateId);
        if (options?.FromVersion > 0)
            cmd.Parameters.AddWithValue("fromVer", options.FromVersion);
        if (options?.EventTypeFilter != null)
            cmd.Parameters.AddWithValue("typeFilter", options.EventTypeFilter);
        if (options?.FromTimestamp != null)
            cmd.Parameters.AddWithValue("fromTs", options.FromTimestamp.Value);

        var events = new List<StoredEvent>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var eventId = reader.GetGuid(0);
            var eventTypeName = reader.GetString(1);
            var aggId = reader.GetGuid(2);
            var aggType = reader.GetString(3);
            var version = reader.GetInt32(4);
            var json = reader.GetString(5);
            var userId = reader.GetGuid(6);
            var sessionId = reader.GetGuid(7);
            var headersJson = reader.IsDBNull(8) ? null : reader.GetString(8);
            var ts = reader.GetDateTime(9);

            var type = Type.GetType(eventTypeName);
            if (type == null) continue;
            if (JsonSerializer.Deserialize(json, type) is not IDomainEvent domainEvent) continue;

            var metadata = new EventMetadata
            {
                EventId = eventId,
                EventType = eventTypeName,
                AggregateId = aggId,
                Version = version,
                Timestamp = ts,
                UserId = userId,
                GameSessionId = sessionId,
                CustomHeaders = headersJson != null ? JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson) : null
            };

            events.Add(new StoredEvent { DomainEvent = domainEvent, Metadata = metadata });
        }

        return events.Count != 0 ? new EventStream
        {
            AggregateId = aggregateId,
            Version = events.Last().Metadata.Version,
            AggregateType = events.First().Metadata.AggregateType,
            Events = events,
            CreatedAt = events.First().Metadata.Timestamp,
            UpdatedAt = events.Last().Metadata.Timestamp
        } : null;
    }

    // ---------- �������� ----------

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

    public async Task<Snapshot?> GetLatestSnapshotAsync(Guid aggregateId, int maxVersion)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand(@"
            SELECT version, data, created_at FROM snapshots
            WHERE aggregate_id = @aggId AND version <= @maxVer
            ORDER BY version DESC LIMIT 1
        ", conn);
        cmd.Parameters.AddWithValue("aggId", NpgsqlDbType.Uuid, aggregateId);
        cmd.Parameters.AddWithValue("maxVer", maxVersion);
        await using var reader = await cmd.ExecuteReaderAsync();
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

    // ---------- ���������� ������� ----------

    public async Task<IEnumerable<StoredEvent>> GetEventsByTypeAsync(string eventType, DateTime? from = null, DateTime? to = null, int? maxCount = null)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        string where = "WHERE event_type = @type";
        if (from.HasValue) where += " AND timestamp >= @from";
        if (to.HasValue) where += " AND timestamp <= @to";
        string limit = maxCount.HasValue ? $"LIMIT {maxCount.Value}" : "";

        using var cmd = new NpgsqlCommand($@"
            SELECT event_id, event_type, aggregate_id, aggregate_type, version, data,
                   user_id, session_id, custom_headers, timestamp
            FROM events {where} ORDER BY timestamp ASC {limit}
        ", conn);
        cmd.Parameters.AddWithValue("type", eventType);
        if (from.HasValue) cmd.Parameters.AddWithValue("from", from.Value);
        if (to.HasValue) cmd.Parameters.AddWithValue("to", to.Value);

        var result = new List<StoredEvent>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            // ... ����������� ������
            result.Add(ReadStoredEvent(reader));
        }
        return result;
    }

    // ���������� ����� ���������� (����������)
    private async Task SaveInternal<T>(
    T aggregate,
    EventMetadata metadataTemplate,
    CancellationToken cancellationToken = default)
    where T : AggregateRoot, new()
    {
        var events = aggregate.GetUncommittedEvents().ToList();
        if (events.Count == 0) return;

        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        using var tx = await conn.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);

        // ���������� ������ �������� (FOR UPDATE)
        using var lockCmd = new NpgsqlCommand(@"
        SELECT version FROM events
        WHERE aggregate_id = @aggId
        ORDER BY version DESC LIMIT 1
        FOR UPDATE
    ", conn, tx);
        lockCmd.Parameters.AddWithValue("aggId", NpgsqlDbType.Uuid, aggregate.Id);
        var result = await lockCmd.ExecuteScalarAsync(cancellationToken);
        int currentMaxVersion = result as int? ?? 0;

        // �������� ������ (ConsistencyManager ��� ������ ���, �� ������������)
        if (aggregate.OriginalVersion != currentMaxVersion)
        {
            _logger.LogWarning(
                "Concurrency conflict in SaveInternal for aggregate {AggregateId}: expected {Expected}, actual {Actual}",
                aggregate.Id, aggregate.OriginalVersion, currentMaxVersion);
            _metrics.IncrementCounter("dnd.eventstore.concurrency_conflict");
            throw new StateConflictException(aggregate.Id, aggregate.OriginalVersion, currentMaxVersion);
        }

        int nextVersion = currentMaxVersion + 1;
        foreach (var domainEvent in events)
        {
            var metadata = new EventMetadata
            {
                EventId = Guid.NewGuid(),
                EventType = domainEvent.GetType().AssemblyQualifiedName!,
                AggregateId = aggregate.Id,
                Version = nextVersion,
                Timestamp = DateTime.UtcNow,
                UserId = metadataTemplate.UserId,
                GameSessionId = metadataTemplate.GameSessionId,
                CustomHeaders = metadataTemplate.CustomHeaders == null
                    ? null
                    : new Dictionary<string, string>(metadataTemplate.CustomHeaders)
            };

            var json = JsonSerializer.Serialize(domainEvent, domainEvent.GetType());
            var headersJson = metadata.CustomHeaders != null
                ? (object)JsonSerializer.Serialize(metadata.CustomHeaders)
                : DBNull.Value;

            using var cmd = new NpgsqlCommand(@"
            INSERT INTO events (event_id, aggregate_id, aggregate_type, version, event_type, data, user_id, session_id, custom_headers, timestamp)
            VALUES (@eventId, @aggId, @aggType, @ver, @type, @data::jsonb, @userId, @sessionId, @headers::jsonb, @ts)
        ", conn, tx);

            cmd.Parameters.AddWithValue("eventId", NpgsqlDbType.Uuid, metadata.EventId);
            cmd.Parameters.AddWithValue("aggId", NpgsqlDbType.Uuid, aggregate.Id);
            cmd.Parameters.AddWithValue("aggType", typeof(T).Name);
            cmd.Parameters.AddWithValue("ver", nextVersion);
            cmd.Parameters.AddWithValue("type", metadata.EventType);
            cmd.Parameters.AddWithValue("data", json);
            cmd.Parameters.AddWithValue("userId", NpgsqlDbType.Uuid, metadata.UserId);
            cmd.Parameters.AddWithValue("sessionId", NpgsqlDbType.Uuid, metadata.GameSessionId);
            cmd.Parameters.AddWithValue("headers", NpgsqlDbType.Jsonb, headersJson);
            cmd.Parameters.AddWithValue("ts", metadata.Timestamp);

            try
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == "23505") // ���������� �����������
            {
                await tx.RollbackAsync(cancellationToken);
                _logger.LogWarning(
                    ex,
                    "Unique constraint violation (concurrency conflict) for aggregate {AggregateId}, version {Version}",
                    aggregate.Id, nextVersion);
                _metrics.IncrementCounter("dnd.eventstore.concurrency_conflict");
                throw new StateConflictException(aggregate.Id, nextVersion - 1, currentMaxVersion);
            }
            nextVersion++;
        }

        await tx.CommitAsync(cancellationToken);

        // Публикуем сохранённые события в шину — без этого проекции (списки
        // персонажей и т.д.), саги (TradeSaga/QuestSaga/CombatSaga) и WebSocket-
        // уведомления игрокам никогда не узнают о новых событиях, хотя они честно
        // сохранены в Postgres. Публикуем только ПОСЛЕ успешного коммита — событие
        // не может считаться случившимся, пока не сохранено надёжно.
        foreach (var domainEvent in events)
        {
            try
            {
                await _eventBus.PublishAsync(domainEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                // Событие уже надёжно сохранено в Postgres — сбой у одного
                // подписчика (например, в проекции) не должен откатывать уже
                // подтверждённую запись. Логируем и продолжаем: проекция может
                // отстать, но это лучше, чем потерять данные пользователя из-за
                // ошибки в неродственном коде подписчика.
                _logger.LogError(ex, "Failed to publish event {EventType} for aggregate {AggregateId} after commit",
                    domainEvent.GetType().Name, aggregate.Id);
            }
        }

        // Обновляем версию агрегата и очищаем список несохранённых событий
        aggregate.SetVersion(nextVersion - 1);
        aggregate.ClearUncommittedEvents();

        // �������� ��������, ���� �����
        if (await _snapshotStore.ShouldCreateSnapshotAsync(aggregate.Id, aggregate.Version))
        {
            var snapshot = SnapshotStore.CreateSnapshotFromAggregate(aggregate);
            await _snapshotStore.SaveSnapshotAsync(snapshot);
        }
    }

    public async Task<IEnumerable<StoredEvent>> GetEventsBySessionAsync(Guid gameSessionId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand(@"
            SELECT event_id, event_type, aggregate_id, aggregate_type, version, data,
                   user_id, session_id, custom_headers, timestamp
            FROM events WHERE session_id = @sessionId ORDER BY timestamp ASC
        ", conn);
        cmd.Parameters.AddWithValue("sessionId", NpgsqlDbType.Uuid, gameSessionId);

        var result = new List<StoredEvent>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(ReadStoredEvent(reader));
        return result;
    }

    public async Task<int> GetCurrentVersionAsync(Guid aggregateId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand(
            "SELECT COALESCE(MAX(version),0) FROM events WHERE aggregate_id = @aggId", conn);
        cmd.Parameters.AddWithValue("aggId", NpgsqlDbType.Uuid, aggregateId);
        var result = await cmd.ExecuteScalarAsync();
        return result switch
        {
            DBNull => 0,
            null => 0,
            _ => Convert.ToInt32(result)
        };
    }

    // ---------- �������� (��������) ----------

    public async Task SubscribeAsync(Func<StoredEvent, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
    {
        // ��� Postgres ����� ������������ LISTEN/NOTIFY ��� ������������� �������.
        // ��������: ���������� �� ��������� ��� �������� �����������.
        await Task.CompletedTask;
    }

    // ---------- ���������� ������ (��������� ��� �������� �������������) ----------
    public async Task<IEnumerable<object>> GetEvents(Guid aggregateId, int fromVersion = 0)
    {
        var stream = await GetEventStreamAsync(aggregateId, new ReadStreamOptions { FromVersion = fromVersion });
        return stream?.Events.Select(e => e.DomainEvent as object) ?? [];
    }

    public async Task<IEnumerable<object>> GetAllEvents()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand(
            "SELECT event_type, data FROM events ORDER BY id", conn);
        var events = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var typeName = reader.GetString(0);
            var json = reader.GetString(1);
            var type = Type.GetType(typeName);
            if (type != null)
            {
                var @event = JsonSerializer.Deserialize(json, type);
                if (@event != null) events.Add(@event);
            }
        }
        return events;
    }

    // ---------- ��������������� ����� ----------
    private static StoredEvent ReadStoredEvent(NpgsqlDataReader reader)
    {
        var eventId = reader.GetGuid(0);
        var eventTypeName = reader.GetString(1);
        var aggId = reader.GetGuid(2);
        _ = reader.GetString(3);
        var version = reader.GetInt32(4);
        var json = reader.GetString(5);
        var userId = reader.GetGuid(6);
        var sessionId = reader.GetGuid(7);
        var headersJson = reader.IsDBNull(8) ? null : reader.GetString(8);
        var ts = reader.GetDateTime(9);

        var type = Type.GetType(eventTypeName);
        var domainEvent = (IDomainEvent)JsonSerializer.Deserialize(json, type!)!;

        return new StoredEvent
        {
            DomainEvent = domainEvent,
            Metadata = new EventMetadata
            {
                EventId = eventId,
                EventType = eventTypeName,
                AggregateId = aggId,
                Version = version,
                Timestamp = ts,
                UserId = userId,
                GameSessionId = sessionId,
                CustomHeaders = headersJson != null
                    ? JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson)
                    : null
            }
        };
    }

    Task<IEnumerable<object>> IEventStore.GetAllEvents()
        => GetAllEvents();

    Task IEventStore.Save<T>(T aggregate, CancellationToken cancellationToken)
        => Save(aggregate);

    Task<IEnumerable<object>> IEventStore.GetEvents(Guid aggregateId, int fromVersion)
        => GetEvents(aggregateId, fromVersion);

    Task IEventStore.SaveWithMetadata<T>(T aggregate, EventMetadata metadata)
        => SaveWithMetadata(aggregate, metadata);

    Task IEventStore.SaveSnapshotAsync(Snapshot snapshot)
        => SaveSnapshotAsync(snapshot);

    Task<Snapshot?> IEventStore.GetLatestSnapshotAsync(Guid aggregateId, int maxVersion)
        => GetLatestSnapshotAsync(aggregateId, maxVersion);

    Task<IEnumerable<StoredEvent>> IEventStore.GetEventsByTypeAsync(string eventType, DateTime? from, DateTime? to, int? maxCount)
        => GetEventsByTypeAsync(eventType, from, to, maxCount);

    Task<IEnumerable<StoredEvent>> IEventStore.GetEventsBySessionAsync(Guid gameSessionId)
        => GetEventsBySessionAsync(gameSessionId);

    Task<int> IEventStore.GetCurrentVersionAsync(Guid aggregateId)
        => GetCurrentVersionAsync(aggregateId);

    public Task<T?> Load<T>(Guid aggregateId, CancellationToken cancellationToken) where T : AggregateRoot, new()
        => Load<T>(aggregateId);
}