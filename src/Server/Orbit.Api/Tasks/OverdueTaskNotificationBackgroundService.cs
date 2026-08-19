using Orbit.Api.HealthChecks;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks.OverdueNotifications;

namespace Orbit.Api.Tasks;

/// <summary>
/// Periodically checks for task items that have just become overdue and pushes a notification to their
/// owner about each one exactly once - the task-item counterpart of
/// CalendarEventReminderBackgroundService.
/// </summary>
public sealed class OverdueTaskNotificationBackgroundService : BackgroundService
{
    private const string ServiceName = "OverdueTaskNotifications";
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    // Caps how many notifications a single poll sends - protects against a burst of simultaneously
    // overdue items (e.g. many tasks all due at midnight) overwhelming this process; anything beyond the
    // cap is simply picked up on the next minute's poll instead of being dropped.
    private const int MaxNotificationsPerPoll = 100;

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly HostedServiceHealthTracker _healthTracker;
    private readonly ILogger<OverdueTaskNotificationBackgroundService> _logger;

    public OverdueTaskNotificationBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        HostedServiceHealthTracker healthTracker,
        ILogger<OverdueTaskNotificationBackgroundService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _healthTracker = healthTracker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                await NotifyNewlyOverdueTasksAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A single failed poll must not stop this background service - the next tick tries again.
                _logger.LogError(exception, "Failed to send overdue task notifications");
            }

            // Reported even after a failed poll: the loop itself is still alive and will try again,
            // which is exactly what HostedServicesHealthCheck needs to know.
            _healthTracker.ReportHeartbeat(ServiceName);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task NotifyNewlyOverdueTasksAsync(CancellationToken cancellationToken)
    {
        // A fresh DI scope per poll: OverdueTaskNotificationScheduler and its repository are scoped
        // services (backed by OrbitDbContext), while this background service itself is a singleton.
        using var scope = _serviceScopeFactory.CreateScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<OverdueTaskNotificationScheduler>();
        var overdueTaskNotificationRepository = scope.ServiceProvider.GetRequiredService<IOverdueTaskNotificationRepository>();
        var pushNotificationDispatcher = scope.ServiceProvider.GetRequiredService<PushNotificationDispatcher>();

        var newlyOverdueItems = await scheduler.FindNewlyOverdueAsync(DateTimeOffset.UtcNow, cancellationToken, MaxNotificationsPerPoll);
        foreach (var overdueTaskItem in newlyOverdueItems)
        {
            await NotifyOwnerAsync(overdueTaskItem, overdueTaskNotificationRepository, pushNotificationDispatcher, cancellationToken);
        }
    }

    private async Task NotifyOwnerAsync(
        OverdueTaskItem overdueTaskItem,
        IOverdueTaskNotificationRepository overdueTaskNotificationRepository,
        PushNotificationDispatcher pushNotificationDispatcher,
        CancellationToken cancellationToken)
    {
        // Reserves this specific item before doing anything else - the unique index backing
        // TryClaimAsync (see its comment) is the actual concurrency guard, letting more than one
        // instance of this background service poll at the same time in the future without a distributed
        // lock or message queue: whichever instance's claim lands first wins, the other backs off here.
        var claimedAtUtc = DateTimeOffset.UtcNow;
        var claimed = await overdueTaskNotificationRepository.TryClaimAsync(overdueTaskItem.TaskItemId, claimedAtUtc, cancellationToken);
        if (!claimed)
        {
            return;
        }

        try
        {
            var payload = OverdueTaskPushContent.Build(overdueTaskItem);
            await pushNotificationDispatcher.NotifyUserAsync(overdueTaskItem.UserId, payload, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // PushNotificationDispatcher itself never throws (see its class comment) - reaching here
            // means something failed before it, e.g. building the payload. Releasing the claim lets this
            // item be retried on the next poll instead of silently never being notified about.
            _logger.LogError(
                exception, "Failed to send an overdue task notification for task item {TaskItemId}", overdueTaskItem.TaskItemId);
            await overdueTaskNotificationRepository.ReleaseClaimAsync(overdueTaskItem.TaskItemId, cancellationToken);
        }
    }
}
