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
    /// <paramref name="maxResults"/> caps how many reminders a single call returns, protecting against a
    /// burst of simultaneously due reminders overwhelming the caller (e.g. many events all reminding "10
    /// minutes before" clustered around the same time) - anything beyond the cap is simply picked up by
    /// the next call instead of being dropped.
    /// </summary>
    public async Task<IReadOnlyList<DueEventReminder>> FindDueRemindersAsync(
        DateTimeOffset nowUtc, TimeSpan lookBackWindow, CancellationToken cancellationToken, int maxResults = int.MaxValue)
    {
        var candidateEvents = await _eventReminderRepository.GetAllWithRemindersConfiguredAsync(cancellationToken);
        var dueReminders = new List<DueEventReminder>();

        foreach (var calendarEvent in candidateEvents)
        {
            if (WasAllDayEventCreatedOnItsOwnStartDate(calendarEvent))
            {
                continue;
            }

            foreach (var minutesBeforeStart in calendarEvent.Details.ReminderMinutesBeforeStart)
            {
                if (dueReminders.Count >= maxResults)
                {
                    return dueReminders;
                }

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

    /// <summary>
    /// An all-day event created on the same calendar day it starts already told its owner about itself
    /// at creation time (see EventCreationEmailContent) - a same-day "the event is starting" reminder on
    /// top of that would be redundant, so it's suppressed entirely for that event.
    /// </summary>
    private static bool WasAllDayEventCreatedOnItsOwnStartDate(CalendarEvent calendarEvent)
    {
        if (!calendarEvent.Details.IsAllDay)
        {
            return false;
        }

        // Both timestamps are compared as calendar dates in the event start's own offset (rather than
        // each in its own stored offset, or both converted to UTC), so "the same day" means the same
        // thing regardless of which time zone created the event - see CalendarEventEditor.razor's
        // ToDateTimeOffset, which anchors an all-day event's StartUtc to local midnight in the browser's
        // own offset at the moment it was picked.
        var createdAtInStartOffset = calendarEvent.CreatedAtUtc.ToOffset(calendarEvent.Details.StartUtc.Offset);
        return createdAtInStartOffset.Date == calendarEvent.Details.StartUtc.Date;
    }
}
