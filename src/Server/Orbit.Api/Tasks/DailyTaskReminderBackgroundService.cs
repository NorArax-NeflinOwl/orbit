using Orbit.Api.HealthChecks;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks.DailyReminders;
using Orbit.Core.Users;

namespace Orbit.Api.Tasks;

/// <summary>
/// Periodically checks for task items with "remind daily" enabled whose configured time of day has been
/// reached today, and notifies their owner about each one at most once per day - the once-a-day
/// counterpart of OverdueTaskNotificationBackgroundService, which only ever notifies once in total.
/// </summary>
public sealed class DailyTaskReminderBackgroundService : BackgroundService
{
    private const string ServiceName = "DailyTaskReminders";
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan LookBackWindow = TimeSpan.FromMinutes(5);

    // Caps how many reminders a single poll sends - protects against a burst of simultaneously due
    // reminders (e.g. many tasks all set to remind at midnight) overwhelming this process; anything
    // beyond the cap is simply picked up on the next minute's poll instead of being dropped.
    private const int MaxRemindersPerPoll = 100;

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly HostedServiceHealthTracker _healthTracker;
    private readonly ILogger<DailyTaskReminderBackgroundService> _logger;

    public DailyTaskReminderBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        HostedServiceHealthTracker healthTracker,
        ILogger<DailyTaskReminderBackgroundService> logger)
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
                await SendDueRemindersAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A single failed poll must not stop this background service - the next tick tries again.
                _logger.LogError(exception, "Failed to send daily task reminders");
            }

            // Reported even after a failed poll: the loop itself is still alive and will try again,
            // which is exactly what HostedServicesHealthCheck needs to know.
            _healthTracker.ReportHeartbeat(ServiceName);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SendDueRemindersAsync(CancellationToken cancellationToken)
    {
        // A fresh DI scope per poll: DailyTaskReminderScheduler and its repository are scoped services
        // (backed by OrbitDbContext), while this background service itself is a singleton.
        using var scope = _serviceScopeFactory.CreateScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<DailyTaskReminderScheduler>();
        var dailyTaskReminderRepository = scope.ServiceProvider.GetRequiredService<IDailyTaskReminderRepository>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var pushNotificationDispatcher = scope.ServiceProvider.GetRequiredService<PushNotificationDispatcher>();
        var notificationRecorder = scope.ServiceProvider.GetRequiredService<NotificationRecorder>();

        // DailyTaskReminderScheduler compares each candidate against its own local time-of-day (see
        // TaskItem.DailyReminderTimeOfDay), so - like EventReminderEmailContent's own use of
        // .LocalDateTime - "now" here means this server's local time, since the app has no concept of a
        // per-user time zone.
        var dueReminders = await scheduler.FindDueRemindersAsync(
            DateTimeOffset.Now, LookBackWindow, cancellationToken, maxResults: MaxRemindersPerPoll);
        foreach (var dueReminder in dueReminders)
        {
            await SendReminderAsync(
                dueReminder, dailyTaskReminderRepository, userRepository, emailSender, pushNotificationDispatcher, notificationRecorder,
                _logger, cancellationToken);
        }
    }

    private static async Task SendReminderAsync(
        DueDailyTaskReminder dueReminder,
        IDailyTaskReminderRepository dailyTaskReminderRepository,
        IUserRepository userRepository,
        IEmailSender emailSender,
        PushNotificationDispatcher pushNotificationDispatcher,
        NotificationRecorder notificationRecorder,
        ILogger<DailyTaskReminderBackgroundService> logger,
        CancellationToken cancellationToken)
    {
        // Reserves this specific (task item, date) pair before doing anything else - the unique index
        // backing TryClaimAsync (see its comment) is the actual concurrency guard, letting more than one
        // instance of this background service poll at the same time without a distributed lock or message
        // queue: whichever instance's claim lands first wins, the other backs off here.
        var claimedAtUtc = DateTimeOffset.UtcNow;
        var claimed = await dailyTaskReminderRepository.TryClaimAsync(
            dueReminder.TaskItemId, dueReminder.ReminderDate, claimedAtUtc, cancellationToken);
        if (!claimed)
        {
            return;
        }

        // Built unconditionally (not just inside the Push branch below) since the in-app feed entry
        // reuses the same title/body/url a push notification would use, independent of whether push
        // delivery itself ends up allowed.
        var payload = DailyTaskReminderPushContent.Build(dueReminder);
        var recordResult = await notificationRecorder.RecordAndFilterAsync(
            dueReminder.UserId, dueReminder.NotificationChannel, NotificationEntryKind.PushReminder,
            payload.Title, payload.Body, payload.Url, cancellationToken);

        // Sent best-effort per channel, mirroring CalendarEventReminderBackgroundService: the claim above
        // guards the whole (task item, date) pair, not each channel individually, so once at least one
        // notification has gone out the claim must stay in place - releasing it would resend on a later poll.
        // A recorded feed entry counts the same as a channel send here (see NotificationRecordResult) -
        // both globally-disabled delivery channels shouldn't make this reminder look unclaimed again.
        var sentOnAnyChannel = recordResult.EntryRecorded;
        var channel = recordResult.AllowedChannel;

        if (channel.HasFlag(NotificationChannel.Push))
        {
            await pushNotificationDispatcher.NotifyUserAsync(dueReminder.UserId, payload, cancellationToken);
            sentOnAnyChannel = true;
        }

        if (channel.HasFlag(NotificationChannel.Email))
        {
            try
            {
                var owner = await userRepository.GetByIdAsync(dueReminder.UserId, cancellationToken);
                if (owner is not null)
                {
                    var (subject, body) = DailyTaskReminderEmailContent.Build(dueReminder);
                    await emailSender.SendAsync(owner.Email, subject, body, cancellationToken);
                    sentOnAnyChannel = true;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(
                    exception, "Failed to send a daily task reminder e-mail for task item {TaskItemId}", dueReminder.TaskItemId);
            }
        }

        if (!sentOnAnyChannel)
        {
            // Nothing actually went out (a build failure, a missing owner, or the channel had no legs to
            // begin with) - release the claim so today's reminder is retried on the next poll instead of
            // silently never being sent.
            await dailyTaskReminderRepository.ReleaseClaimAsync(dueReminder.TaskItemId, dueReminder.ReminderDate, cancellationToken);
        }
    }
}
