using DbUp;
using Microsoft.Extensions.Logging;

namespace dnd_game.migrations;

public class DatabaseMigrator
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseMigrator> _logger;

    public DatabaseMigrator(string connectionString, ILogger<DatabaseMigrator> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public bool Migrate()
    {
        try
        {
            // Убеждаемся, что база данных существует
            EnsureDatabase.For.PostgresqlDatabase(_connectionString);

            var upgrader = DeployChanges.To
                .PostgresqlDatabase(_connectionString)  // ← теперь этот метод доступен
                .WithScriptsFromFileSystem(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "migrations"))
                .WithTransaction()
                .LogToConsole()
                .Build();

            var result = upgrader.PerformUpgrade();

            if (!result.Successful)
            {
                _logger.LogError(result.Error, "Database migration failed.");
                return false;
            }

            _logger.LogInformation("Database migration completed successfully.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration failed with exception.");
            return false;
        }
    }
}