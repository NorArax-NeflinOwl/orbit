namespace Orbit.Api.RateLimiting;

/// <summary>
/// Deletes rate-limiting windows that have closed. The shared count has to be written down to be shared
/// (see <see cref="PostgresRateLimitWindows"/>), and a row is created per caller per window, so without
/// this OS_RATE_LIMITS would grow for the life of the installation to hold counts nothing will ever read
/// again.
///
/// Hourly, and deleting only windows that closed an hour ago, so a sweep can never take a window
/// somebody is still inside: the longest window any policy uses is a minute, and an hour of slack is
/// far more than the difference between two machines' clocks could ever be.
///
/// Like the other retention sweeps this reports no heartbeat to HostedServiceHealthTracker - the health
/// check treats every heartbeat as stale after two minutes, so an hourly one would report the whole API
/// unhealthy and have the container restarted.
/// </summary>
public sealed class RateLimitWindowRetentionBackgroundService(
    IRateLimitWindows windows,
    ILogger<RateLimitWindowRetentionBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

    private static readonly TimeSpan KeepClosedWindowsFor = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        do
        {
            try
            {
                var deletedCount = await windows.DeleteWindowsClosedBeforeAsync(
                    DateTimeOffset.UtcNow - KeepClosedWindowsFor, stoppingToken);

                if (deletedCount > 0)
                {
                    logger.LogInformation("Deleted {DeletedCount} closed rate limit windows", deletedCount);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A single failed sweep must not stop this background service - the next tick tries again.
                logger.LogError(exception, "Failed to delete closed rate limit windows");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
