using Orbit.Api.HealthChecks;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks.OverdueNotifications;
using Orbit.Core.Users;

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
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var pushNotificationDispatcher = scope.ServiceProvider.GetRequiredService<PushNotificationDispatcher>();
        var notificationRecorder = scope.ServiceProvider.GetRequiredService<NotificationRecorder>();

        var newlyOverdueItems = await scheduler.FindNewlyOverdueAsync(DateTimeOffset.UtcNow, cancellationToken, MaxNotificationsPerPoll);
        foreach (var overdueTaskItem in newlyOverdueItems)
        {
            await NotifyOwnerAsync(
                overdueTaskItem, overdueTaskNotificationRepository, userRepository, emailSender, pushNotificationDispatcher,
                notificationRecorder, cancellationToken);
        }
    }

    private async Task NotifyOwnerAsync(
        OverdueTaskItem overdueTaskItem,
        IOverdueTaskNotificationRepository overdueTaskNotificationRepository,
        IUserRepository userRepository,
        IEmailSender emailSender,
        PushNotificationDispatcher pushNotificationDispatcher,
        NotificationRecorder notificationRecorder,
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

        // Built unconditionally (not just inside the Push branch below) since the in-app feed entry
        // reuses the same title/body/url a push notification would use, independent of whether push
        // delivery itself ends up allowed.
        var payload = OverdueTaskPushContent.Build(overdueTaskItem);
        var recordResult = await notificationRecorder.RecordAndFilterAsync(
            overdueTaskItem.UserId, overdueTaskItem.NotificationChannel, NotificationEntryKind.PushReminder,
            payload, cancellationToken);

        // Sent best-effort per channel, mirroring CalendarEventReminderBackgroundService: the claim above
        // guards the whole item, not each channel individually, so once at least one notification has
        // gone out the claim must stay in place - releasing it would make a later poll resend it. A
        // recorded feed entry counts the same as a channel send here (see NotificationRecordResult) -
        // both globally-disabled delivery channels shouldn't make this item look unclaimed again.
        var sentOnAnyChannel = recordResult.EntryRecorded;
        var channel = recordResult.AllowedChannel;

        if (channel.HasFlag(NotificationChannel.Push))
        {
            await pushNotificationDispatcher.NotifyUserAsync(overdueTaskItem.UserId, payload, cancellationToken);
            sentOnAnyChannel = true;
        }

        if (channel.HasFlag(NotificationChannel.Email))
        {
            try
            {
                var owner = await userRepository.GetByIdAsync(overdueTaskItem.UserId, cancellationToken);
                if (owner is not null)
                {
                    var (subject, body) = OverdueTaskEmailContent.Build(overdueTaskItem);
                    await emailSender.SendAsync(owner.Email, subject, body, cancellationToken);
                    sentOnAnyChannel = true;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception, "Failed to send an overdue task e-mail for task item {TaskItemId}", overdueTaskItem.TaskItemId);
            }
        }

        if (!sentOnAnyChannel)
        {
            // Nothing actually went out (a build failure, a missing owner, or the channel had no legs to
            // begin with) - release the claim so this item is retried on the next poll instead of
            // silently never being notified about.
            await overdueTaskNotificationRepository.ReleaseClaimAsync(overdueTaskItem.TaskItemId, cancellationToken);
        }
    }
}
