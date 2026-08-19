// infrastructure/security/refresh_token_store.cs
using Npgsql;

namespace dnd_game.Infrastructure.Security
{
    /// <summary>
    /// Хранилище refresh-токенов. Отделено от TokenService, чтобы токены переживали
    /// перезапуск процесса и были общими для нескольких инстансов сервиса.
    /// </summary>
    public interface IRefreshTokenStore
    {
        Task SaveAsync(RefreshTokenEntry entry, CancellationToken cancellationToken = default);
        Task<RefreshTokenEntry?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
        Task RevokeAsync(string tokenHash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Отзыв всех refresh-токенов пользователя (например, "выйти на всех устройствах"
        /// или принудительный logout при компрометации аккаунта).
        /// </summary>
        Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Удаляет уже истёкшие токены. Предназначен для периодической фоновой очистки
        /// (см. RefreshTokenCleanupService), чтобы таблица не росла бесконечно.
        /// </summary>
        Task<int> DeleteExpiredAsync(CancellationToken cancellationToken = default);
    }

    public class PostgresRefreshTokenStore : IRefreshTokenStore
    {
        private readonly string _connectionString;

        public PostgresRefreshTokenStore(string connectionString)
        {
            _connectionString = connectionString;
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand(@"
                CREATE TABLE IF NOT EXISTS refresh_tokens (
                    token_hash TEXT PRIMARY KEY,
                    user_id UUID NOT NULL,
                    device_info TEXT NULL,
                    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                    expires_at TIMESTAMPTZ NOT NULL,
                    is_revoked BOOLEAN NOT NULL DEFAULT FALSE
                );
                CREATE INDEX IF NOT EXISTS idx_refresh_tokens_user_id ON refresh_tokens(user_id);
                CREATE INDEX IF NOT EXISTS idx_refresh_tokens_expires_at ON refresh_tokens(expires_at);
            ", conn);
            cmd.ExecuteNonQuery();
        }

        public async Task SaveAsync(RefreshTokenEntry entry, CancellationToken cancellationToken = default)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO refresh_tokens (token_hash, user_id, device_info, expires_at, is_revoked)
                VALUES (@token_hash, @user_id, @device_info, @expires_at, @is_revoked)
                ON CONFLICT (token_hash) DO UPDATE
                SET user_id = EXCLUDED.user_id,
                    device_info = EXCLUDED.device_info,
                    expires_at = EXCLUDED.expires_at,
                    is_revoked = EXCLUDED.is_revoked
            ", conn);
            cmd.Parameters.AddWithValue("token_hash", entry.TokenHash);
            cmd.Parameters.AddWithValue("user_id", entry.UserId);
            cmd.Parameters.AddWithValue("device_info", (object?)entry.DeviceInfo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("expires_at", entry.ExpiresAt);
            cmd.Parameters.AddWithValue("is_revoked", entry.IsRevoked);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<RefreshTokenEntry?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            using var cmd = new NpgsqlCommand(@"
                SELECT token_hash, user_id, device_info, expires_at, is_revoked
                FROM refresh_tokens WHERE token_hash = @token_hash
            ", conn);
            cmd.Parameters.AddWithValue("token_hash", tokenHash);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new RefreshTokenEntry
                {
                    TokenHash = reader.GetString(0),
                    UserId = reader.GetGuid(1),
                    DeviceInfo = reader.IsDBNull(2) ? null : reader.GetString(2),
                    ExpiresAt = reader.GetDateTime(3),
                    IsRevoked = reader.GetBoolean(4)
                };
            }
            return null;
        }

        public async Task RevokeAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            using var cmd = new NpgsqlCommand("UPDATE refresh_tokens SET is_revoked = TRUE WHERE token_hash = @token_hash", conn);
            cmd.Parameters.AddWithValue("token_hash", tokenHash);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            using var cmd = new NpgsqlCommand("UPDATE refresh_tokens SET is_revoked = TRUE WHERE user_id = @user_id", conn);
            cmd.Parameters.AddWithValue("user_id", userId);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<int> DeleteExpiredAsync(CancellationToken cancellationToken = default)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            using var cmd = new NpgsqlCommand("DELETE FROM refresh_tokens WHERE expires_at < NOW()", conn);
            return await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
