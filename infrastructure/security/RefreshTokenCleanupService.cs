// infrastructure/security/RefreshTokenCleanupService.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace dnd_game.Infrastructure.Security;

/// <summary>
/// Фоновый сервис для периодической очистки истёкших refresh-токенов.
/// </summary>
public class RefreshTokenCleanupService : BackgroundService
{
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly ILogger<RefreshTokenCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1); // можно вынести в настройки

    public RefreshTokenCleanupService(
        IRefreshTokenStore refreshTokenStore,
        ILogger<RefreshTokenCleanupService> logger)
    {
        _refreshTokenStore = refreshTokenStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RefreshTokenCleanupService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);
                var deleted = await _refreshTokenStore.DeleteExpiredAsync(stoppingToken);
                if (deleted > 0)
                    _logger.LogInformation("Deleted {Count} expired refresh tokens.", deleted);
            }
            catch (OperationCanceledException)
            {
                // Ожидаемая отмена при остановке сервиса
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning expired refresh tokens.");
            }
        }

        _logger.LogInformation("RefreshTokenCleanupService stopped.");
    }
}