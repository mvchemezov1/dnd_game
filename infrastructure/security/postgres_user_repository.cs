// infrastructure/security/postgres_user_repository.cs
using System.Text.Json;
using Npgsql;
using dnd_game.Application.Security;

namespace dnd_game.Infrastructure.Security;

public class PostgresUserRepository : IUserRepository
{
    private readonly string _connectionString;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PostgresUserRepository(string connectionString)
    {
        _connectionString = connectionString;
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS users (
                id UUID PRIMARY KEY,
                username TEXT UNIQUE NOT NULL,
                email TEXT UNIQUE NOT NULL,
                password_hash TEXT NOT NULL,
                global_role TEXT NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                is_active BOOLEAN NOT NULL DEFAULT TRUE,
                campaign_roles JSONB DEFAULT '{}'::jsonb
            );
        ", conn);
        cmd.ExecuteNonQuery();
    }

    public async Task<UserAccount?> GetByIdAsync(Guid userId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand(@"
            SELECT id, username, email, password_hash, global_role, created_at, is_active, campaign_roles
            FROM users WHERE id = @id
        ", conn);
        cmd.Parameters.AddWithValue("id", userId);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapUser(reader);
        }
        return null;
    }

    public async Task<UserAccount?> GetByUsernameAsync(string username)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand(@"
            SELECT id, username, email, password_hash, global_role, created_at, is_active, campaign_roles
            FROM users WHERE username = @username
        ", conn);
        cmd.Parameters.AddWithValue("username", username);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapUser(reader);
        }
        return null;
    }

    public async Task<UserAccount?> GetByEmailAsync(string email)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand(@"
            SELECT id, username, email, password_hash, global_role, created_at, is_active, campaign_roles
            FROM users WHERE email = @email
        ", conn);
        cmd.Parameters.AddWithValue("email", email);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapUser(reader);
        }
        return null;
    }

    public async Task AddAsync(UserAccount user)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO users (id, username, email, password_hash, global_role, created_at, is_active, campaign_roles)
            VALUES (@id, @username, @email, @password_hash, @global_role, @created_at, @is_active, @campaign_roles::jsonb)
        ", conn);
        cmd.Parameters.AddWithValue("id", user.Id);
        cmd.Parameters.AddWithValue("username", user.Username);
        cmd.Parameters.AddWithValue("email", user.Email);
        cmd.Parameters.AddWithValue("password_hash", user.PasswordHash);
        cmd.Parameters.AddWithValue("global_role", user.GlobalRole.ToString());
        cmd.Parameters.AddWithValue("created_at", user.CreatedAt);
        cmd.Parameters.AddWithValue("is_active", user.IsActive);
        var rolesJson = JsonSerializer.Serialize(user.CampaignRoles ?? new Dictionary<Guid, CampaignRole>(), _jsonOptions);
        cmd.Parameters.AddWithValue("campaign_roles", NpgsqlTypes.NpgsqlDbType.Jsonb, rolesJson);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateAsync(UserAccount user)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand(@"
            UPDATE users
            SET username = @username,
                email = @email,
                password_hash = @password_hash,
                global_role = @global_role,
                is_active = @is_active,
                campaign_roles = @campaign_roles::jsonb
            WHERE id = @id
        ", conn);
        cmd.Parameters.AddWithValue("id", user.Id);
        cmd.Parameters.AddWithValue("username", user.Username);
        cmd.Parameters.AddWithValue("email", user.Email);
        cmd.Parameters.AddWithValue("password_hash", user.PasswordHash);
        cmd.Parameters.AddWithValue("global_role", user.GlobalRole.ToString());
        cmd.Parameters.AddWithValue("is_active", user.IsActive);
        var rolesJson = JsonSerializer.Serialize(user.CampaignRoles ?? new Dictionary<Guid, CampaignRole>(), _jsonOptions);
        cmd.Parameters.AddWithValue("campaign_roles", NpgsqlTypes.NpgsqlDbType.Jsonb, rolesJson);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(Guid userId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand("DELETE FROM users WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static UserAccount MapUser(NpgsqlDataReader reader)
    {
        var id = reader.GetGuid(0);
        var username = reader.GetString(1);
        var email = reader.GetString(2);
        var passwordHash = reader.GetString(3);
        var globalRole = Enum.Parse<UserRole>(reader.GetString(4));
        var createdAt = reader.GetDateTime(5);
        var isActive = reader.GetBoolean(6);
        var campaignRolesJson = reader.IsDBNull(7) ? "{}" : reader.GetString(7);
        var campaignRoles = JsonSerializer.Deserialize<Dictionary<Guid, CampaignRole>>(campaignRolesJson) ?? new();

        return new UserAccount
        {
            Id = id,
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            GlobalRole = globalRole,
            CreatedAt = createdAt,
            IsActive = isActive,
            CampaignRoles = campaignRoles
        };
    }
}