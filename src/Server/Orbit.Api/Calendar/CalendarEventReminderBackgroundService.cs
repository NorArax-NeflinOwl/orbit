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

        var dueReminders = await scheduler.FindDueRemindersAsync(
            DateTimeOffset.UtcNow, LookBackWindow, cancellationToken, maxResults: MaxRemindersPerPoll);
        foreach (var dueReminder in dueReminders)
        {
            await SendReminderEmailAsync(
                dueReminder, userRepository, calendarEventShareRepository, eventReminderRepository, emailSender,
                pushNotificationDispatcher, _logger, cancellationToken);
        }
    }

    private static async Task SendReminderEmailAsync(
        DueEventReminder dueReminder,
        IUserRepository userRepository,
        ICalendarEventShareRepository calendarEventShareRepository,
        IEventReminderRepository eventReminderRepository,
        IEmailSender emailSender,
        PushNotificationDispatcher pushNotificationDispatcher,
        ILogger<CalendarEventReminderBackgroundService> logger,
        CancellationToken cancellationToken)
    {
        var calendarEvent = dueReminder.CalendarEvent;

        // Reserves this specific reminder before doing anything else - the unique index backing
        // TryClaimAsync (see its comment) is the actual concurrency guard, letting more than one
        // instance of this background service poll at the same time in the future without a distributed
        // lock or message queue: whichever instance's claim lands first wins, the other backs off here.
        var claimedAtUtc = DateTimeOffset.UtcNow;
        var claimed = await eventReminderRepository.TryClaimAsync(
            calendarEvent.Id, dueReminder.MinutesBeforeStart, dueReminder.OccurrenceStartUtc, claimedAtUtc, cancellationToken);
        if (!claimed)
        {
            return;
        }

        var recipients = await ResolveRecipientsAsync(calendarEvent, userRepository, calendarEventShareRepository, cancellationToken);
        if (recipients.Count == 0)
        {
            // The owning account was deleted after the event was created, and no guest has accepted a
            // share of it either - nothing meaningful to notify, and no one to ever notify, so the claim
            // stays in place rather than being retried.
            return;
        }

        var occurrenceDetails = BuildOccurrenceDetails(calendarEvent.Details, dueReminder.OccurrenceStartUtc);
        var channel = calendarEvent.Details.ReminderNotificationChannel;

        // Sent best-effort per recipient: the claim above guards the whole event+lead-time pair, not
        // each recipient individually (see TryClaimAsync), so once at least one notification has gone
        // out the claim must stay in place - releasing it would make a later poll resend to whoever
        // already received it. Only when every single recipient's e-mail fails (or the channel has no
        // e-mail leg to begin with) does nothing go out, making a full retry on the next poll safe -
        // PushNotificationDispatcher never throws, so a push send always counts as delivered here.
        var sentToAnyRecipient = false;
        foreach (var recipient in recipients)
        {
            if (channel.HasFlag(NotificationChannel.Email))
            {
                try
                {
                    var (subject, body) = EventReminderEmailContent.Build(occurrenceDetails, dueReminder.MinutesBeforeStart);
                    await emailSender.SendAsync(recipient.Email, subject, body, cancellationToken);
                    sentToAnyRecipient = true;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(
                        exception, "Failed to send calendar event reminder e-mail to user {RecipientUserId} for event {EventId}",
                        recipient.Id, calendarEvent.Id);
                }
            }

            if (channel.HasFlag(NotificationChannel.Push))
            {
                var pushPayload = EventReminderPushContent.Build(occurrenceDetails, calendarEvent.Id, dueReminder.MinutesBeforeStart);
                await pushNotificationDispatcher.NotifyUserAsync(recipient.Id, pushPayload, cancellationToken);
                sentToAnyRecipient = true;
            }
        }

        if (!sentToAnyRecipient)
        {
            await eventReminderRepository.ReleaseClaimAsync(
                calendarEvent.Id, dueReminder.MinutesBeforeStart, dueReminder.OccurrenceStartUtc, cancellationToken);
        }
    }

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
