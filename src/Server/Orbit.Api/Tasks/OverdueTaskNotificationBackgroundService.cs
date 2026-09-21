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

        // One notice per owner rather than one per entry - see SeveralEntriesAtOnce for what the second
        // notice of a minute costs the reader.
        foreach (var owner in newlyOverdueItems.GroupBy(overdueTaskItem => overdueTaskItem.UserId))
        {
            var claimedItems = await ClaimAsync(owner, overdueTaskNotificationRepository, cancellationToken);
            if (claimedItems.Count == 0)
            {
                continue;
            }

            await NotifyOwnerAsync(
                owner.Key, claimedItems, overdueTaskNotificationRepository, userRepository, emailSender,
                pushNotificationDispatcher, notificationRecorder, cancellationToken);
        }
    }

    /// <summary>
    /// Reserves each of one owner's newly overdue entries before anything is sent, and answers with the
    /// ones this poll won - the unique index backing TryClaimAsync (see its comment) is the actual
    /// concurrency guard, letting more than one instance of this background service poll at the same
    /// time in the future without a distributed lock or message queue: whichever instance's claim lands
    /// first wins, the other backs off here. Claimed one at a time even though the notice is collective,
    /// so an entry another instance is already speaking about simply stays out of this one.
    /// </summary>
    private static async Task<IReadOnlyList<OverdueTaskItem>> ClaimAsync(
        IEnumerable<OverdueTaskItem> overdueTaskItems,
        IOverdueTaskNotificationRepository overdueTaskNotificationRepository,
        CancellationToken cancellationToken)
    {
        var claimedItems = new List<OverdueTaskItem>();
        foreach (var overdueTaskItem in overdueTaskItems)
        {
            var claimedAtUtc = DateTimeOffset.UtcNow;
            if (await overdueTaskNotificationRepository.TryClaimAsync(overdueTaskItem.TaskItemId, claimedAtUtc, cancellationToken))
            {
                claimedItems.Add(overdueTaskItem);
            }
        }

        return claimedItems;
    }

    private async Task NotifyOwnerAsync(
        Guid userId,
        IReadOnlyList<OverdueTaskItem> claimedItems,
        IOverdueTaskNotificationRepository overdueTaskNotificationRepository,
        IUserRepository userRepository,
        IEmailSender emailSender,
        PushNotificationDispatcher pushNotificationDispatcher,
        NotificationRecorder notificationRecorder,
        CancellationToken cancellationToken)
    {
        // Built unconditionally (not just inside the Push branch below) since the in-app feed entry
        // reuses the same title/body/url a push notification would use, independent of whether push
        // delivery itself ends up allowed. It names every claimed entry, whatever channel each of them
        // asked for: the feed is where the reader looks back, and an entry missing from it is an entry
        // that was never announced at all.
        var payload = OverdueTaskPushContent.Build(claimedItems);
        var recordResult = await notificationRecorder.RecordAndFilterAsync(
            userId, ChannelsAskedFor(claimedItems), NotificationEntryKind.PushReminder, payload, cancellationToken);

        // Sent best-effort per channel, mirroring CalendarEventReminderBackgroundService: a claim guards
        // its whole entry, not each channel individually, so once something has gone out about an entry
        // its claim must stay in place - releasing it would make a later poll resend it. A recorded feed
        // entry counts the same as a channel send here (see NotificationRecordResult) - both
        // globally-disabled delivery channels shouldn't make these items look unclaimed again.
        var spokenFor = new HashSet<Guid>();
        if (recordResult.EntryRecorded)
        {
            spokenFor.UnionWith(claimedItems.Select(overdueTaskItem => overdueTaskItem.TaskItemId));
        }

        var channel = recordResult.AllowedChannel;

        if (channel.HasFlag(NotificationChannel.Push) && Asking(claimedItems, NotificationChannel.Push) is { Count: > 0 } toPush)
        {
            await pushNotificationDispatcher.NotifyUserAsync(userId, OverdueTaskPushContent.Build(toPush), cancellationToken);
            spokenFor.UnionWith(toPush.Select(overdueTaskItem => overdueTaskItem.TaskItemId));
        }

        if (channel.HasFlag(NotificationChannel.Email) && Asking(claimedItems, NotificationChannel.Email) is { Count: > 0 } toEmail)
        {
            spokenFor.UnionWith(await EmailOwnerAsync(userId, toEmail, userRepository, emailSender, cancellationToken));
        }

        foreach (var unannounced in claimedItems.Where(overdueTaskItem => !spokenFor.Contains(overdueTaskItem.TaskItemId)))
        {
            // Nothing actually went out about this one (a missing owner, a failed e-mail, or the channel
            // had no legs to begin with) - release its claim so it is retried on the next poll instead of
            // silently never being notified about.
            await overdueTaskNotificationRepository.ReleaseClaimAsync(unannounced.TaskItemId, cancellationToken);
        }
    }

    /// <summary>
    /// Answers with the entries the e-mail actually named, which is none of them when there is nobody to
    /// send it to or the send fails. A failed e-mail is logged rather than thrown: the other channels
    /// have already spoken, and the entries it would have named are simply left unclaimed for next poll.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> EmailOwnerAsync(
        Guid userId,
        IReadOnlyList<OverdueTaskItem> overdueTaskItems,
        IUserRepository userRepository,
        IEmailSender emailSender,
        CancellationToken cancellationToken)
    {
        try
        {
            var owner = await userRepository.GetByIdAsync(userId, cancellationToken);
            if (owner is null)
            {
                return [];
            }

            var (subject, body) = OverdueTaskEmailContent.Build(overdueTaskItems);
            await emailSender.SendAsync(owner.Email, subject, body, cancellationToken);
            return [.. overdueTaskItems.Select(overdueTaskItem => overdueTaskItem.TaskItemId)];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Failed to send an overdue task e-mail to user {UserId}", userId);
            return [];
        }
    }

    /// <summary>Every channel any of these entries asks for, which is what the account's own settings filter.</summary>
    private static NotificationChannel ChannelsAskedFor(IReadOnlyList<OverdueTaskItem> overdueTaskItems)
        => overdueTaskItems.Aggregate(
            NotificationChannel.None, (channels, overdueTaskItem) => channels | overdueTaskItem.NotificationChannel);

    /// <summary>
    /// The entries whose own setting asks for this channel - see TaskItemReminders.WhenOverdue. One
    /// collective notice still says only what the entries in it chose to be told on: an entry set to
    /// e-mail only is named in the e-mail and not in the push.
    /// </summary>
    private static List<OverdueTaskItem> Asking(IReadOnlyList<OverdueTaskItem> overdueTaskItems, NotificationChannel channel)
        => [.. overdueTaskItems.Where(overdueTaskItem => overdueTaskItem.NotificationChannel.HasFlag(channel))];
}
