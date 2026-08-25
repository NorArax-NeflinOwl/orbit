using Orbit.Core.Notifications;

namespace Orbit.Api.Notifications;

/// <summary>
/// Deletes notifications that have outlived the retention window their own reader chose (see
/// NotificationSettings.RetentionDays). Clearing the panel only hides an entry, so without this the
/// feed would grow forever and a notification would outlive the thing it was about.
///
/// Hourly rather than by the minute: the window is measured in days, so the worst this costs is an
/// entry surviving up to an hour past its deadline. That cadence is also why this reports no heartbeat
/// to HostedServiceHealthTracker, unlike the delivery loops - the health check treats every heartbeat
/// it is given as stale after two minutes, so an hourly one would report the whole API unhealthy and
/// have the container restarted. A missed sweep costs nothing a user can see, which is the opposite of
/// what that check is for.
/// </summary>
public sealed class NotificationRetentionBackgroundService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

    /// <summary>Applies to a user who has never saved notification settings - the same window Default gives them.</summary>
    private static readonly TimeSpan DefaultRetention = TimeSpan.FromDays(NotificationSettings.DefaultRetentionDays);

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<NotificationRetentionBackgroundService> _logger;

    public NotificationRetentionBackgroundService(
        IServiceScopeFactory serviceScopeFactory, ILogger<NotificationRetentionBackgroundService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        do
        {
            try
            {
                await DeleteExpiredNotificationsAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A single failed sweep must not stop this background service - the next tick tries again.
                _logger.LogError(exception, "Failed to delete expired notifications");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DeleteExpiredNotificationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var notificationEntryRepository = scope.ServiceProvider.GetRequiredService<INotificationEntryRepository>();

        var deletedCount = await notificationEntryRepository.DeleteExpiredAsync(
            DateTimeOffset.UtcNow, DefaultRetention, cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation("Deleted {DeletedCount} expired notification entries", deletedCount);
        }
    }
}
