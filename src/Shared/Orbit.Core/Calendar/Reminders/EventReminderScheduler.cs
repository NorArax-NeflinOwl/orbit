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
    /// A reminder is due once its lead time before an occurrence's start has been reached, and hasn't been
    /// due for longer than <paramref name="lookBackWindow"/> - the window bounds how late a missed
    /// reminder can still fire (e.g. after the app was briefly down), rather than silently emailing
    /// reminders for occurrences that started long ago the first time this runs after a longer outage.
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
            foreach (var occurrenceStartUtc in RelevantOccurrenceStarts(calendarEvent, nowUtc, lookBackWindow))
            {
                if (WasAllDayEventCreatedOnItsOwnStartDate(calendarEvent, occurrenceStartUtc))
                {
                    continue;
                }

                foreach (var minutesBeforeStart in calendarEvent.Details.ReminderLeadTimesMinutes)
                {
                    if (dueReminders.Count >= maxResults)
                    {
                        return dueReminders;
                    }

                    if (!IsDue(occurrenceStartUtc, minutesBeforeStart, nowUtc, lookBackWindow))
                    {
                        continue;
                    }

                    if (await _eventReminderRepository.HasBeenSentAsync(calendarEvent.Id, minutesBeforeStart, occurrenceStartUtc, cancellationToken))
                    {
                        continue;
                    }

                    dueReminders.Add(new DueEventReminder(calendarEvent, minutesBeforeStart, occurrenceStartUtc));
                }
            }
        }

        return dueReminders;
    }

    /// <summary>
    /// The occurrence start times worth checking for calendarEvent right now: just its own Details.StartUtc
    /// for a non-recurring event, or every occurrence whose reminder could plausibly be due for a recurring
    /// one - i.e. whose start falls within lookBackWindow before, or this event's furthest lead time after,
    /// nowUtc. Recurring events aren't expanded server-side beyond that narrow window (see
    /// CalendarEventOccurrenceGenerator): there's no reason to compute occurrences years away just to
    /// immediately discard them as not due.
    /// </summary>
    private static IEnumerable<DateTimeOffset> RelevantOccurrenceStarts(CalendarEvent calendarEvent, DateTimeOffset nowUtc, TimeSpan lookBackWindow)
    {
        if (calendarEvent.Details.Recurrence is not { } recurrence)
        {
            return [calendarEvent.Details.StartUtc];
        }

        var leadTimesMinutes = calendarEvent.Details.ReminderLeadTimesMinutes;
        var windowStart = nowUtc - lookBackWindow + TimeSpan.FromMinutes(leadTimesMinutes.Min());
        var windowEndExclusive = nowUtc + TimeSpan.FromMinutes(leadTimesMinutes.Max()) + TimeSpan.FromTicks(1);
        return CalendarEventOccurrenceGenerator.GenerateOccurrenceStarts(calendarEvent.Details.StartUtc, recurrence, windowStart, windowEndExclusive);
    }

    private static bool IsDue(DateTimeOffset occurrenceStartUtc, int minutesBeforeStart, DateTimeOffset nowUtc, TimeSpan lookBackWindow)
    {
        var reminderAtUtc = occurrenceStartUtc.AddMinutes(-minutesBeforeStart);
        return reminderAtUtc <= nowUtc && reminderAtUtc >= nowUtc - lookBackWindow;
    }

    /// <summary>
    /// An all-day event created on the same calendar day one of its occurrences starts already told its
    /// owner about itself at creation time (see EventCreationEmailContent) - a same-day "the event is
    /// starting" reminder on top of that would be redundant, so it's suppressed for that occurrence. For a
    /// recurring event this only ever matches its very first occurrence: CreatedAtUtc is fixed, so a later
    /// occurrence's date coincides with it only by construction, never by accident.
    /// </summary>
    private static bool WasAllDayEventCreatedOnItsOwnStartDate(CalendarEvent calendarEvent, DateTimeOffset occurrenceStartUtc)
    {
        if (!calendarEvent.Details.IsAllDay)
        {
            return false;
        }

        // Both timestamps are compared as calendar dates in the occurrence's own offset (rather than
        // each in its own stored offset, or both converted to UTC), so "the same day" means the same
        // thing regardless of which time zone created the event - see CalendarEventEditor.razor's
        // ToDateTimeOffset, which anchors an all-day event's StartUtc to local midnight in the browser's
        // own offset at the moment it was picked.
        var createdAtInOccurrenceOffset = calendarEvent.CreatedAtUtc.ToOffset(occurrenceStartUtc.Offset);
        return createdAtInOccurrenceOffset.Date == occurrenceStartUtc.Date;
    }
}
