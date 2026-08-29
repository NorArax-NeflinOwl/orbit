using Orbit.Contracts.Calendar;
using Orbit.Core.Calendar;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Screens.Calendar;

/// <summary>
/// A repeating event on every day it actually falls on, rather than only the one it is stored under.
///
/// The API returns a recurring event as the single record it is - the repeat is a rule, not rows - and
/// the phone drew it exactly once, at its start. A weekly standup appeared in the week it began and
/// never again, which reads the same as an event that had stopped. Orbit.Web's grid has expanded
/// repeats all along.
///
/// The stepping is Orbit.Core's rather than written again here: the phone references the domain, which
/// is what makes this the second implementation and not the third. Orbit.Web keeps its own copy
/// (CalendarEventOccurrenceExpander) only because the browser client cannot reference Orbit.Core.
/// </summary>
public static class CalendarOccurrences
{
    /// <summary>
    /// Every event as the days it lands on. One that does not repeat comes back unchanged; one that does
    /// comes back as a copy per occurrence, its times shifted and its duration kept.
    ///
    /// A copy carries the original's ids, so opening one opens the event it is an occurrence of - there
    /// is only one event to open, and editing it changes every occurrence, which is what a rule means.
    /// </summary>
    /// <remarks>
    /// A few occurrences from just before <paramref name="windowStart"/> can come back too, which is the
    /// generator's own contract; callers here are already asking about a particular day or month, so
    /// they filter one out without noticing it.
    /// </remarks>
    public static IReadOnlyList<LocalCalendarEvent> Between(
        IReadOnlyList<LocalCalendarEvent> events, DateTimeOffset windowStart, DateTimeOffset windowEndExclusive)
        => [.. events.SelectMany(calendarEvent => OccurrencesOf(calendarEvent, windowStart, windowEndExclusive))];

    private static IEnumerable<LocalCalendarEvent> OccurrencesOf(
        LocalCalendarEvent calendarEvent, DateTimeOffset windowStart, DateTimeOffset windowEndExclusive)
    {
        if (RuleOf(calendarEvent.Details.Recurrence) is not { } recurrence)
        {
            return [calendarEvent];
        }

        var details = calendarEvent.Details;
        var duration = details.EndUtc - details.StartUtc;

        return CalendarEventOccurrenceGenerator
            .GenerateOccurrenceStarts(details.StartUtc, recurrence, windowStart, windowEndExclusive)
            .Select(start => Copy(
                calendarEvent, details with { StartUtc = start, EndUtc = start + duration }));
    }

    /// <summary>
    /// The stored rule as the domain states it, or null when there is none - and also when the phone
    /// cannot make sense of what it holds. An unreadable frequency is drawn once, where it starts, which
    /// is what happened to every repeat before this existed and is the safe half of being wrong.
    /// </summary>
    private static EventRecurrence? RuleOf(RecurrenceDto? recurrence)
        => recurrence is not null
            && Enum.TryParse<RecurrenceFrequency>(recurrence.Frequency, ignoreCase: true, out var frequency)
            ? new EventRecurrence(frequency, recurrence.IntervalCount, recurrence.UntilUtc)
            : null;

    /// <summary>
    /// The same event at another time. Detached from the store on purpose: these are drawn, never saved,
    /// and a copy that found its way back would turn one repeating event into a calendar full of them.
    /// </summary>
    private static LocalCalendarEvent Copy(LocalCalendarEvent calendarEvent, CalendarEventDetailsDto occurrence)
        => new()
        {
            LocalId = calendarEvent.LocalId,
            ServerId = calendarEvent.ServerId,
            Details = occurrence,
            CreatedAtUtc = calendarEvent.CreatedAtUtc,
            UpdatedAtUtc = calendarEvent.UpdatedAtUtc,
            IsShared = calendarEvent.IsShared,
            SharedByUserName = calendarEvent.SharedByUserName,
            IsSharedWithOthers = calendarEvent.IsSharedWithOthers,
            AccessLevel = calendarEvent.AccessLevel,
            OwnerUserId = calendarEvent.OwnerUserId,
            LastSyncedAtUtc = calendarEvent.LastSyncedAtUtc
        };
}
