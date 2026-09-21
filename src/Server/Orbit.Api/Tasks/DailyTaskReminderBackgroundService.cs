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

        // One reminder per owner rather than one per entry - see SeveralEntriesAtOnce for what the
        // second notice of a minute costs the reader.
        foreach (var owner in dueReminders.GroupBy(dueReminder => dueReminder.UserId))
        {
            var claimedReminders = await ClaimAsync(owner, dailyTaskReminderRepository, cancellationToken);
            if (claimedReminders.Count == 0)
            {
                continue;
            }

            await SendReminderAsync(
                owner.Key, claimedReminders, dailyTaskReminderRepository, userRepository, emailSender,
                pushNotificationDispatcher, notificationRecorder, _logger, cancellationToken);
        }
    }

    /// <summary>
    /// Reserves each of one owner's due reminders before anything is sent, and answers with the ones
    /// this poll won - the unique index backing TryClaimAsync (see its comment) is the actual
    /// concurrency guard, letting more than one instance of this background service poll at the same
    /// time without a distributed lock or message queue: whichever instance's claim lands first wins,
    /// the other backs off here. Claimed one at a time even though the reminder is collective, so an
    /// entry another instance is already speaking about simply stays out of this one.
    ///
    /// Only what comes round again is brought back, and it is brought back before the reminder goes out
    /// so the notification is about something still to do. An ordinary errand is not touched at all: it
    /// is asked about until it is done, and a reminder that un-ticked it would be the app taking the
    /// reader's answer away - see DailyTaskReminderCandidate.ComesRoundAgain, and the decision recorded
    /// in info/future-plan.md. Its deadline is left alone for the same reason.
    /// </summary>
    private static async Task<IReadOnlyList<DueDailyTaskReminder>> ClaimAsync(
        IEnumerable<DueDailyTaskReminder> dueReminders,
        IDailyTaskReminderRepository dailyTaskReminderRepository,
        CancellationToken cancellationToken)
    {
        var claimedReminders = new List<DueDailyTaskReminder>();
        foreach (var dueReminder in dueReminders)
        {
            var claimedAtUtc = DateTimeOffset.UtcNow;
            var claimed = await dailyTaskReminderRepository.TryClaimAsync(
                dueReminder.TaskItemId, dueReminder.ReminderDate, claimedAtUtc, cancellationToken);
            if (!claimed)
            {
                continue;
            }

            if (dueReminder.ComesRoundAgain)
            {
                await dailyTaskReminderRepository.ReopenAsync(
                    dueReminder.TaskItemId, dueReminder.ReminderDate, cancellationToken);
            }

            claimedReminders.Add(dueReminder);
        }

        return claimedReminders;
    }

    private static async Task SendReminderAsync(
        Guid userId,
        IReadOnlyList<DueDailyTaskReminder> claimedReminders,
        IDailyTaskReminderRepository dailyTaskReminderRepository,
        IUserRepository userRepository,
        IEmailSender emailSender,
        PushNotificationDispatcher pushNotificationDispatcher,
        NotificationRecorder notificationRecorder,
        ILogger<DailyTaskReminderBackgroundService> logger,
        CancellationToken cancellationToken)
    {
        // Built unconditionally (not just inside the Push branch below) since the in-app feed entry
        // reuses the same title/body/url a push notification would use, independent of whether push
        // delivery itself ends up allowed. It names every claimed entry, whatever channel each of them
        // asked for: the feed is where the reader looks back, and an entry missing from it is an entry
        // that was never announced at all.
        var payload = DailyTaskReminderPushContent.Build(claimedReminders);
        var recordResult = await notificationRecorder.RecordAndFilterAsync(
            userId, ChannelsAskedFor(claimedReminders), NotificationEntryKind.PushReminder, payload, cancellationToken);

        // Sent best-effort per channel, mirroring CalendarEventReminderBackgroundService: a claim guards
        // its whole (task item, date) pair, not each channel individually, so once something has gone out
        // about an entry its claim must stay in place - releasing it would resend on a later poll. A
        // recorded feed entry counts the same as a channel send here (see NotificationRecordResult) -
        // both globally-disabled delivery channels shouldn't make these reminders look unclaimed again.
        var spokenFor = new HashSet<Guid>();
        if (recordResult.EntryRecorded)
        {
            spokenFor.UnionWith(claimedReminders.Select(dueReminder => dueReminder.TaskItemId));
        }

        var channel = recordResult.AllowedChannel;

        if (channel.HasFlag(NotificationChannel.Push) && Asking(claimedReminders, NotificationChannel.Push) is { Count: > 0 } toPush)
        {
            await pushNotificationDispatcher.NotifyUserAsync(userId, DailyTaskReminderPushContent.Build(toPush), cancellationToken);
            spokenFor.UnionWith(toPush.Select(dueReminder => dueReminder.TaskItemId));
        }

        if (channel.HasFlag(NotificationChannel.Email) && Asking(claimedReminders, NotificationChannel.Email) is { Count: > 0 } toEmail)
        {
            spokenFor.UnionWith(await EmailOwnerAsync(userId, toEmail, userRepository, emailSender, logger, cancellationToken));
        }

        foreach (var unannounced in claimedReminders.Where(dueReminder => !spokenFor.Contains(dueReminder.TaskItemId)))
        {
            // Nothing actually went out about this one (a missing owner, a failed e-mail, or the channel
            // had no legs to begin with) - release its claim so today's reminder is retried on the next
            // poll instead of silently never being sent.
            await dailyTaskReminderRepository.ReleaseClaimAsync(
                unannounced.TaskItemId, unannounced.ReminderDate, cancellationToken);
        }
    }

    /// <summary>
    /// Answers with the entries the e-mail actually named, which is none of them when there is nobody to
    /// send it to or the send fails. A failed e-mail is logged rather than thrown: the other channels
    /// have already spoken, and the entries it would have named are simply left unclaimed for next poll.
    /// </summary>
    private static async Task<IReadOnlyList<Guid>> EmailOwnerAsync(
        Guid userId,
        IReadOnlyList<DueDailyTaskReminder> dueReminders,
        IUserRepository userRepository,
        IEmailSender emailSender,
        ILogger<DailyTaskReminderBackgroundService> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var owner = await userRepository.GetByIdAsync(userId, cancellationToken);
            if (owner is null)
            {
                return [];
            }

            var (subject, body) = DailyTaskReminderEmailContent.Build(dueReminders);
            await emailSender.SendAsync(owner.Email, subject, body, cancellationToken);
            return [.. dueReminders.Select(dueReminder => dueReminder.TaskItemId)];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to send a daily task reminder e-mail to user {UserId}", userId);
            return [];
        }
    }

    /// <summary>Every channel any of these reminders asks for, which is what the account's own settings filter.</summary>
    private static NotificationChannel ChannelsAskedFor(IReadOnlyList<DueDailyTaskReminder> dueReminders)
        => dueReminders.Aggregate(
            NotificationChannel.None, (channels, dueReminder) => channels | dueReminder.NotificationChannel);

    /// <summary>
    /// The reminders whose own setting asks for this channel - see TaskItemReminders.DailyChannel. One
    /// collective reminder still says only what the entries in it chose to be told on: an entry set to
    /// e-mail only is named in the e-mail and not in the push.
    /// </summary>
    private static List<DueDailyTaskReminder> Asking(
        IReadOnlyList<DueDailyTaskReminder> dueReminders, NotificationChannel channel)
        => [.. dueReminders.Where(dueReminder => dueReminder.NotificationChannel.HasFlag(channel))];
}
