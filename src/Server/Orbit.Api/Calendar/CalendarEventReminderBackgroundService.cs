using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orbit.Api.HealthChecks;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.Reminders;
using Orbit.Core.Notifications;
using Orbit.Core.Users;

namespace Orbit.Api.Calendar;

/// <summary>
/// Periodically checks for calendar event reminders that have come due and emails the event's owner,
/// plus every guest who has accepted a share of the event, about each one exactly once - and, for
/// whichever of those recipients has push notifications enabled, sends a push notification alongside the
/// email (see PushNotificationDispatcher). Lives entirely in Orbit.Api: sending a real email needs an
/// SMTP connection and credentials that must never reach the Blazor WebAssembly client (see
/// GeocodingApiClient's class comment in Orbit.Web for the same reasoning applied to a different
/// third-party call).
/// </summary>
public sealed class CalendarEventReminderBackgroundService : BackgroundService
{
    private const string ServiceName = "CalendarEventReminders";
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan LookBackWindow = TimeSpan.FromMinutes(5);

    // Caps how many reminder emails a single poll sends. Protects against a burst of simultaneously due
    // reminders (e.g. many events all set to remind "10 minutes before", clustered around the same
    // time) overwhelming the SMTP server or this process; anything beyond the cap is simply picked up on
    // the next minute's poll instead of being dropped.
    private const int MaxRemindersPerPoll = 100;

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly HostedServiceHealthTracker _healthTracker;
    private readonly ILogger<CalendarEventReminderBackgroundService> _logger;

    public CalendarEventReminderBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        HostedServiceHealthTracker healthTracker,
        ILogger<CalendarEventReminderBackgroundService> logger)
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
                // A single failed poll (e.g. the SMTP server is temporarily unreachable) must not stop
                // this background service - the next tick tries again.
                _logger.LogError(exception, "Failed to send calendar event reminder emails");
            }

            // Reported even after a failed poll: the loop itself is still alive and will try again,
            // which is exactly what HostedServicesHealthCheck needs to know.
            _healthTracker.ReportHeartbeat(ServiceName);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SendDueRemindersAsync(CancellationToken cancellationToken)
    {
        // A fresh DI scope per poll: EventReminderScheduler and its repositories are scoped services
        // (backed by OrbitDbContext), while this background service itself is a singleton.
        using var scope = _serviceScopeFactory.CreateScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<EventReminderScheduler>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var calendarEventShareRepository = scope.ServiceProvider.GetRequiredService<ICalendarEventShareRepository>();
        var eventReminderRepository = scope.ServiceProvider.GetRequiredService<IEventReminderRepository>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var pushNotificationDispatcher = scope.ServiceProvider.GetRequiredService<PushNotificationDispatcher>();
        var notificationRecorder = scope.ServiceProvider.GetRequiredService<NotificationRecorder>();

        var dueReminders = await scheduler.FindDueRemindersAsync(
            DateTimeOffset.UtcNow, LookBackWindow, cancellationToken, maxResults: MaxRemindersPerPoll);

        // Claimed one reminder at a time, then told one reader at a time: an event's reminder reaches
        // its owner and every guest, and a reader with two appointments at nine gets one notice naming
        // both rather than two a second apart - the second of which nobody sees, for the reason
        // EventReminderPushContent's list overload gives.
        var toTell = new List<(DueEventReminder Reminder, EventReminderOccurrence Occurrence, User Recipient)>();
        var withSomebodyToTell = new List<DueEventReminder>();
        foreach (var dueReminder in dueReminders)
        {
            var calendarEvent = dueReminder.CalendarEvent;

            // Reserves this specific reminder before doing anything else - the unique index backing
            // TryClaimAsync (see its comment) is the actual concurrency guard, letting more than one
            // instance of this background service poll at the same time in the future without a
            // distributed lock or message queue: whichever instance's claim lands first wins, the other
            // backs off here.
            var claimedAtUtc = DateTimeOffset.UtcNow;
            var claimed = await eventReminderRepository.TryClaimAsync(
                calendarEvent.Id, dueReminder.MinutesBeforeStart, dueReminder.OccurrenceStartUtc, claimedAtUtc, cancellationToken);
            if (!claimed)
            {
                continue;
            }

            var recipients = await ResolveRecipientsAsync(calendarEvent, userRepository, calendarEventShareRepository, cancellationToken);
            if (recipients.Count == 0)
            {
                // The owning account was deleted after the event was created, and no guest has accepted a
                // share of it either - nothing meaningful to notify, and no one to ever notify, so the
                // claim stays in place rather than being retried.
                continue;
            }

            var occurrence = new EventReminderOccurrence(
                BuildOccurrenceDetails(calendarEvent.Details, dueReminder.OccurrenceStartUtc), calendarEvent.Id,
                dueReminder.MinutesBeforeStart);
            withSomebodyToTell.Add(dueReminder);
            toTell.AddRange(recipients.Select(recipient => (dueReminder, occurrence, recipient)));
        }

        var spokenFor = new HashSet<DueEventReminder>();
        foreach (var reader in toTell.GroupBy(told => told.Recipient.Id))
        {
            spokenFor.UnionWith(await TellAsync(
                reader.First().Recipient, [.. reader.Select(told => (told.Reminder, told.Occurrence))], emailSender,
                pushNotificationDispatcher, notificationRecorder, _logger, cancellationToken));
        }

        // A claim guards the whole event+lead-time pair, not each reader (see TryClaimAsync), so once
        // anything has gone out about a reminder to anybody its claim stays in place - releasing it would
        // make a later poll resend to whoever already received it. Only a reminder nothing at all went
        // out about is released, making a full retry on the next poll safe.
        foreach (var unannounced in withSomebodyToTell.Where(dueReminder => !spokenFor.Contains(dueReminder)))
        {
            await eventReminderRepository.ReleaseClaimAsync(
                unannounced.CalendarEvent.Id, unannounced.MinutesBeforeStart, unannounced.OccurrenceStartUtc, cancellationToken);
        }
    }

    /// <summary>
    /// Everything due for one reader in this poll, as one notice on each channel, and which reminders
    /// something actually went out about. A recorded feed entry counts as having gone out (see
    /// NotificationRecordResult), and so does a push - PushNotificationDispatcher never throws. It names
    /// every reminder whatever channel each asked for, since the feed is where the reader looks back;
    /// the push and the e-mail each name only the reminders that asked for them.
    /// </summary>
    private static async Task<IReadOnlyList<DueEventReminder>> TellAsync(
        User recipient,
        IReadOnlyList<(DueEventReminder Reminder, EventReminderOccurrence Occurrence)> reminders,
        IEmailSender emailSender,
        PushNotificationDispatcher pushNotificationDispatcher,
        NotificationRecorder notificationRecorder,
        ILogger<CalendarEventReminderBackgroundService> logger,
        CancellationToken cancellationToken)
    {
        var channelsAskedFor = reminders.Aggregate(
            NotificationChannel.None, (channels, each) => channels | ChannelOf(each.Reminder));

        // Built unconditionally (not just inside the Push branch below) since the in-app feed entry
        // reuses the same title/body/url a push notification would use, independent of whether push
        // delivery itself ends up allowed for this reader.
        var recordResult = await notificationRecorder.RecordAndFilterAsync(
            recipient.Id, channelsAskedFor, NotificationEntryKind.PushReminder,
            EventReminderPushContent.Build([.. reminders.Select(each => each.Occurrence)]), cancellationToken);

        var spokenFor = new List<DueEventReminder>();
        if (recordResult.EntryRecorded)
        {
            spokenFor.AddRange(reminders.Select(each => each.Reminder));
        }

        var allowed = recordResult.AllowedChannel;

        if (allowed.HasFlag(NotificationChannel.Email) && Asking(reminders, NotificationChannel.Email) is { Count: > 0 } toEmail)
        {
            try
            {
                var (subject, body) = EventReminderEmailContent.Build([.. toEmail.Select(each => each.Occurrence)]);
                await emailSender.SendAsync(recipient.Email, subject, body, cancellationToken);
                spokenFor.AddRange(toEmail.Select(each => each.Reminder));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(
                    exception, "Failed to send calendar event reminder e-mail to user {RecipientUserId}", recipient.Id);
            }
        }

        if (allowed.HasFlag(NotificationChannel.Push) && Asking(reminders, NotificationChannel.Push) is { Count: > 0 } toPush)
        {
            await pushNotificationDispatcher.NotifyUserAsync(
                recipient.Id, EventReminderPushContent.Build([.. toPush.Select(each => each.Occurrence)]), cancellationToken);
            spokenFor.AddRange(toPush.Select(each => each.Reminder));
        }

        return spokenFor;
    }

    /// <summary>The channel the event asked its reminders to arrive on - an event-wide setting, not a per-guest one.</summary>
    private static NotificationChannel ChannelOf(DueEventReminder dueReminder)
        => dueReminder.CalendarEvent.Details.ReminderNotificationChannel;

    /// <summary>The reminders whose event asks for this channel: an event set to e-mail only is named in the e-mail and not in the push.</summary>
    private static List<(DueEventReminder Reminder, EventReminderOccurrence Occurrence)> Asking(
        IReadOnlyList<(DueEventReminder Reminder, EventReminderOccurrence Occurrence)> reminders, NotificationChannel channel)
        => [.. reminders.Where(each => ChannelOf(each.Reminder).HasFlag(channel))];

    /// <summary>
    /// The event details to notify about: as stored, for a non-recurring event or a recurring event's very
    /// first occurrence, or shifted onto occurrenceStartUtc (preserving the event's own duration) so the
    /// reminder's subject/body/push payload reflect the occurrence that is actually due rather than always
    /// the series' original start time.
    /// </summary>
    private static CalendarEventDetails BuildOccurrenceDetails(CalendarEventDetails details, DateTimeOffset occurrenceStartUtc)
    {
        if (occurrenceStartUtc == details.StartUtc)
        {
            return details;
        }

        var duration = details.EndUtc - details.StartUtc;
        return details with { StartUtc = occurrenceStartUtc, EndUtc = occurrenceStartUtc + duration };
    }

    /// <summary>
    /// The event's owner, plus every guest who has accepted a share of the event (see
    /// CalendarEventShare.IsAccepted) - an invited guest who never accepted isn't included, since they
    /// never added the event to their own calendar. A deleted account is silently skipped rather than
    /// surfaced as a broken recipient.
    /// </summary>
    private static async Task<IReadOnlyList<User>> ResolveRecipientsAsync(
        CalendarEvent calendarEvent, IUserRepository userRepository, ICalendarEventShareRepository calendarEventShareRepository,
        CancellationToken cancellationToken)
    {
        var recipients = new List<User>();

        var owner = await userRepository.GetByIdAsync(calendarEvent.UserId, cancellationToken);
        if (owner is not null)
        {
            recipients.Add(owner);
        }

        var acceptedGuestUserIds = await calendarEventShareRepository.GetAcceptedRecipientUserIdsAsync(calendarEvent.Id, cancellationToken);
        foreach (var guestUserId in acceptedGuestUserIds)
        {
            var guest = await userRepository.GetByIdAsync(guestUserId, cancellationToken);
            if (guest is not null)
            {
                recipients.Add(guest);
            }
        }

        return recipients;
    }
}
