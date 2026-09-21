using Orbit.Api.HealthChecks;
using Orbit.Core.Inventories.ExpiryReminders;
using Orbit.Core.Notifications;
using Orbit.Core.Users;

namespace Orbit.Api.Inventories;

/// <summary>
/// Periodically checks for inventory items nearing their expiry date and warns their owner about each
/// one once per (item, expiry date) pair - the inventory counterpart of
/// OverdueTaskNotificationBackgroundService/CalendarEventReminderBackgroundService.
/// </summary>
public sealed class InventoryExpiryReminderBackgroundService : BackgroundService
{
    private const string ServiceName = "InventoryExpiryReminders";
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    // Caps how many warnings a single poll sends - protects against a burst of simultaneously
    // near-expiry items overwhelming this process; anything beyond the cap is simply picked up on the
    // next minute's poll instead of being dropped.
    private const int MaxRemindersPerPoll = 100;

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly HostedServiceHealthTracker _healthTracker;
    private readonly ILogger<InventoryExpiryReminderBackgroundService> _logger;

    public InventoryExpiryReminderBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        HostedServiceHealthTracker healthTracker,
        ILogger<InventoryExpiryReminderBackgroundService> logger)
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
                await SendExpiryRemindersAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A single failed poll must not stop this background service - the next tick tries again.
                _logger.LogError(exception, "Failed to send inventory expiry reminders");
            }

            // Reported even after a failed poll: the loop itself is still alive and will try again,
            // which is exactly what HostedServicesHealthCheck needs to know.
            _healthTracker.ReportHeartbeat(ServiceName);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SendExpiryRemindersAsync(CancellationToken cancellationToken)
    {
        // A fresh DI scope per poll: InventoryExpiryReminderScheduler and its repository are scoped
        // services (backed by OrbitDbContext), while this background service itself is a singleton.
        using var scope = _serviceScopeFactory.CreateScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<InventoryExpiryReminderScheduler>();
        var inventoryExpiryNotificationRepository = scope.ServiceProvider.GetRequiredService<IInventoryExpiryNotificationRepository>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var pushNotificationDispatcher = scope.ServiceProvider.GetRequiredService<PushNotificationDispatcher>();
        var notificationRecorder = scope.ServiceProvider.GetRequiredService<NotificationRecorder>();

        var dueReminders = await scheduler.FindDueRemindersAsync(DateTimeOffset.UtcNow, cancellationToken, MaxRemindersPerPoll);

        // One warning per owner rather than one per thing - see InventoryExpiryPushContent's list overload
        // for what the second warning of a minute costs the reader.
        foreach (var owner in dueReminders.GroupBy(reminder => reminder.UserId))
        {
            var claimedReminders = await ClaimAsync(owner, inventoryExpiryNotificationRepository, cancellationToken);
            if (claimedReminders.Count == 0)
            {
                continue;
            }

            await NotifyOwnerAsync(
                owner.Key, claimedReminders, inventoryExpiryNotificationRepository, userRepository, emailSender,
                pushNotificationDispatcher, notificationRecorder, cancellationToken);
        }
    }

    /// <summary>
    /// Reserves each of one owner's (item, expiry date) pairs before anything is sent, and answers with
    /// the ones this poll won - the unique index backing TryClaimAsync (see its comment) is the actual
    /// concurrency guard, letting more than one instance of this background service poll at the same
    /// time without a distributed lock or message queue: whichever instance's claim lands first wins,
    /// the other backs off here. Claimed one at a time even though the warning is collective, so a thing
    /// another instance is already warning about simply stays out of this one.
    /// </summary>
    private static async Task<IReadOnlyList<DueExpiryReminder>> ClaimAsync(
        IEnumerable<DueExpiryReminder> dueReminders,
        IInventoryExpiryNotificationRepository inventoryExpiryNotificationRepository,
        CancellationToken cancellationToken)
    {
        var claimedReminders = new List<DueExpiryReminder>();
        foreach (var reminder in dueReminders)
        {
            var claimedAtUtc = DateTimeOffset.UtcNow;
            if (await inventoryExpiryNotificationRepository.TryClaimAsync(
                reminder.InventoryItemId, reminder.ExpiryDate, claimedAtUtc, cancellationToken))
            {
                claimedReminders.Add(reminder);
            }
        }

        return claimedReminders;
    }

    private async Task NotifyOwnerAsync(
        Guid userId,
        IReadOnlyList<DueExpiryReminder> claimedReminders,
        IInventoryExpiryNotificationRepository inventoryExpiryNotificationRepository,
        IUserRepository userRepository,
        IEmailSender emailSender,
        PushNotificationDispatcher pushNotificationDispatcher,
        NotificationRecorder notificationRecorder,
        CancellationToken cancellationToken)
    {
        // Built unconditionally (not just inside the Push branch below) since the in-app feed entry
        // reuses the same title/body/url a push notification would use, independent of whether push
        // delivery itself ends up allowed. It names every claimed thing, whatever channel each asked for:
        // the feed is where the reader looks back, and a thing missing from it was never warned about.
        var payload = InventoryExpiryPushContent.Build(claimedReminders);
        var recordResult = await notificationRecorder.RecordAndFilterAsync(
            userId, ChannelsAskedFor(claimedReminders), NotificationEntryKind.PushReminder, payload, cancellationToken);

        // Sent best-effort per channel, mirroring OverdueTaskNotificationBackgroundService: a claim guards
        // its whole (item, expiry date) pair, not each channel individually, so once something has gone
        // out about a thing its claim must stay in place - releasing it would make a later poll resend
        // it. A recorded feed entry counts the same as a channel send here (see NotificationRecordResult).
        var spokenFor = new HashSet<Guid>();
        if (recordResult.EntryRecorded)
        {
            spokenFor.UnionWith(claimedReminders.Select(reminder => reminder.InventoryItemId));
        }

        var channel = recordResult.AllowedChannel;

        if (channel.HasFlag(NotificationChannel.Push) && Asking(claimedReminders, NotificationChannel.Push) is { Count: > 0 } toPush)
        {
            await pushNotificationDispatcher.NotifyUserAsync(userId, InventoryExpiryPushContent.Build(toPush), cancellationToken);
            spokenFor.UnionWith(toPush.Select(reminder => reminder.InventoryItemId));
        }

        if (channel.HasFlag(NotificationChannel.Email) && Asking(claimedReminders, NotificationChannel.Email) is { Count: > 0 } toEmail)
        {
            spokenFor.UnionWith(await EmailOwnerAsync(userId, toEmail, userRepository, emailSender, cancellationToken));
        }

        foreach (var unannounced in claimedReminders.Where(reminder => !spokenFor.Contains(reminder.InventoryItemId)))
        {
            // Nothing actually went out about this one (a missing owner, a failed e-mail, or the channel
            // had no legs to begin with) - release its claim so it is retried on the next poll instead of
            // silently never being warned about.
            await inventoryExpiryNotificationRepository.ReleaseClaimAsync(
                unannounced.InventoryItemId, unannounced.ExpiryDate, cancellationToken);
        }
    }

    /// <summary>
    /// Answers with the things the e-mail actually named, which is none of them when there is nobody to
    /// send it to or the send fails. A failed e-mail is logged rather than thrown: the other channels
    /// have already spoken, and the things it would have named are simply left unclaimed for next poll.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> EmailOwnerAsync(
        Guid userId,
        IReadOnlyList<DueExpiryReminder> reminders,
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

            var (subject, body) = InventoryExpiryEmailContent.Build(reminders);
            await emailSender.SendAsync(owner.Email, subject, body, cancellationToken);
            return [.. reminders.Select(reminder => reminder.InventoryItemId)];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Failed to send an inventory expiry e-mail to user {UserId}", userId);
            return [];
        }
    }

    /// <summary>Every channel any of these things asks for, which is what the account's own settings filter.</summary>
    private static NotificationChannel ChannelsAskedFor(IReadOnlyList<DueExpiryReminder> reminders)
        => reminders.Aggregate(NotificationChannel.None, (channels, reminder) => channels | reminder.NotificationChannel);

    /// <summary>
    /// The things whose own setting asks for this channel. One collective warning still says only what
    /// the things in it chose to be told on: a thing set to e-mail only is named in the e-mail and not in
    /// the push.
    /// </summary>
    private static List<DueExpiryReminder> Asking(IReadOnlyList<DueExpiryReminder> reminders, NotificationChannel channel)
        => [.. reminders.Where(reminder => reminder.NotificationChannel.HasFlag(channel))];
}
