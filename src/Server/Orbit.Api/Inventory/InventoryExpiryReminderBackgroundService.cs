using Orbit.Api.HealthChecks;
using Orbit.Core.Inventory.ExpiryReminders;
using Orbit.Core.Notifications;
using Orbit.Core.Users;

namespace Orbit.Api.Inventory;

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
        foreach (var reminder in dueReminders)
        {
            await NotifyOwnerAsync(
                reminder, inventoryExpiryNotificationRepository, userRepository, emailSender, pushNotificationDispatcher,
                notificationRecorder, cancellationToken);
        }
    }

    private async Task NotifyOwnerAsync(
        DueExpiryReminder reminder,
        IInventoryExpiryNotificationRepository inventoryExpiryNotificationRepository,
        IUserRepository userRepository,
        IEmailSender emailSender,
        PushNotificationDispatcher pushNotificationDispatcher,
        NotificationRecorder notificationRecorder,
        CancellationToken cancellationToken)
    {
        // Reserves this specific (item, expiry date) pair before doing anything else - the unique index
        // backing TryClaimAsync (see its comment) is the actual concurrency guard, letting more than one
        // instance of this background service poll at the same time without a distributed lock or
        // message queue: whichever instance's claim lands first wins, the other backs off here.
        var claimedAtUtc = DateTimeOffset.UtcNow;
        var claimed = await inventoryExpiryNotificationRepository.TryClaimAsync(
            reminder.InventoryItemId, reminder.ExpiryDate, claimedAtUtc, cancellationToken);
        if (!claimed)
        {
            return;
        }

        // Built unconditionally (not just inside the Push branch below) since the in-app feed entry
        // reuses the same title/body/url a push notification would use, independent of whether push
        // delivery itself ends up allowed.
        var payload = InventoryExpiryPushContent.Build(reminder);
        var recordResult = await notificationRecorder.RecordAndFilterAsync(
            reminder.UserId, reminder.NotificationChannel, NotificationEntryKind.PushReminder,
            payload.Title, payload.Body, payload.Url, cancellationToken);

        // Sent best-effort per channel, mirroring OverdueTaskNotificationBackgroundService: the claim
        // above guards the whole (item, expiry date) pair, not each channel individually, so once at
        // least one notification has gone out the claim must stay in place - releasing it would make a
        // later poll resend it. A recorded feed entry counts the same as a channel send here (see
        // NotificationRecordResult).
        var sentOnAnyChannel = recordResult.EntryRecorded;
        var channel = recordResult.AllowedChannel;

        if (channel.HasFlag(NotificationChannel.Push))
        {
            await pushNotificationDispatcher.NotifyUserAsync(reminder.UserId, payload, cancellationToken);
            sentOnAnyChannel = true;
        }

        if (channel.HasFlag(NotificationChannel.Email))
        {
            try
            {
                var owner = await userRepository.GetByIdAsync(reminder.UserId, cancellationToken);
                if (owner is not null)
                {
                    var (subject, body) = InventoryExpiryEmailContent.Build(reminder);
                    await emailSender.SendAsync(owner.Email, subject, body, cancellationToken);
                    sentOnAnyChannel = true;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception, "Failed to send an inventory expiry e-mail for item {InventoryItemId}", reminder.InventoryItemId);
            }
        }

        if (!sentOnAnyChannel)
        {
            // Nothing actually went out (a build failure, a missing owner, or the channel had no legs to
            // begin with) - release the claim so this item is retried on the next poll instead of
            // silently never being warned about.
            await inventoryExpiryNotificationRepository.ReleaseClaimAsync(reminder.InventoryItemId, reminder.ExpiryDate, cancellationToken);
        }
    }
}
