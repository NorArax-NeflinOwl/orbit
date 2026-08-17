namespace Orbit.Core.Calendar.Reminders;

/// <summary>
/// Finds calendar event reminders that are due to be sent right now and haven't been sent yet - the
/// core logic behind CalendarEventReminderBackgroundService in Orbit.Api, kept independent of ASP.NET
/// Core hosting so it can be unit tested directly.
/// </summary>
public sealed class EventReminderScheduler
{
    private readonly IEventReminderRepository _eventReminderRepository;

    public EventReminderScheduler(IEventReminderRepository eventReminderRepository)
    {
        _eventReminderRepository = eventReminderRepository;
    }

    /// <summary>
    /// A reminder is due once its lead time before the event's start has been reached, and hasn't been
    /// due for longer than <paramref name="lookBackWindow"/> - the window bounds how late a missed
    /// reminder can still fire (e.g. after the app was briefly down), rather than silently emailing
    /// reminders for events that started long ago the first time this runs after a longer outage.
    /// </summary>
    public async Task<IReadOnlyList<DueEventReminder>> FindDueRemindersAsync(
        DateTimeOffset nowUtc, TimeSpan lookBackWindow, CancellationToken cancellationToken)
    {
        var candidateEvents = await _eventReminderRepository.GetAllWithRemindersConfiguredAsync(cancellationToken);
        var dueReminders = new List<DueEventReminder>();

        foreach (var calendarEvent in candidateEvents)
        {
            foreach (var minutesBeforeStart in calendarEvent.Details.ReminderMinutesBeforeStart)
            {
                if (!IsDue(calendarEvent.Details.StartUtc, minutesBeforeStart, nowUtc, lookBackWindow))
                {
                    continue;
                }

                if (await _eventReminderRepository.HasBeenSentAsync(calendarEvent.Id, minutesBeforeStart, cancellationToken))
                {
                    continue;
                }

                dueReminders.Add(new DueEventReminder(calendarEvent, minutesBeforeStart));
            }
        }

        return dueReminders;
    }

    private static bool IsDue(DateTimeOffset eventStartUtc, int minutesBeforeStart, DateTimeOffset nowUtc, TimeSpan lookBackWindow)
    {
        var reminderAtUtc = eventStartUtc.AddMinutes(-minutesBeforeStart);
        return reminderAtUtc <= nowUtc && reminderAtUtc >= nowUtc - lookBackWindow;
    }
}
