using Orbit.Contracts.Tasks;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Calendar;

/// <summary>
/// A task entry falling due on a day, shown on the calendar beside the events. A deadline is as much a
/// thing happening on a day as an appointment is, and the phone's calendar showed only the appointments
/// - so a week with three things due looked empty. Orbit.Web's calendar has shown both all along.
/// </summary>
/// <param name="TaskListLocalId">Which list this sits on, which is what the entry's own screen is opened by.</param>
/// <param name="ItemId">Which entry it is - a press opens the entry itself, see CalendarViewModel.OpenDeadline.</param>
/// <param name="When">Already in the reader's calendar, so the row itself needs no dictionary.</param>
public sealed record CalendarDeadline(
    Guid TaskListLocalId, Guid ItemId, string ListTitle, string Description, DateTime DueLocalDate, string When,
    bool IsCompleted)
{
    /// <summary>How it reads on the calendar: the list it is on, then what it says.</summary>
    public string Label => ListTitle.Length == 0 ? Description : $"{ListTitle}: {Description}";

    /// <summary>
    /// Every entry with a date, whichever list it sits on. Ones already done are kept rather than
    /// dropped: a day whose errands are all ticked reads differently from an empty one, which is why
    /// Orbit.Web strikes them through rather than hiding them.
    ///
    /// An entry tied to an event is left off the day that event is already on: it <b>is</b> that
    /// appointment, and drawing both writes the same thing twice, one line under the other. It stays on
    /// any other day, and it stays when the event is one this phone has not got - there, nothing on the
    /// day stands for it, and hiding it would lose the appointment rather than tidy it.
    /// </summary>
    public static IReadOnlyList<CalendarDeadline> From(
        IReadOnlyList<LocalTaskList> taskLists, IReadOnlyList<LocalCalendarEvent> events,
        Translations translations)
    {
        // Asked of every day the event is drawn on, a repeat's included: the caller hands over the
        // occurrences rather than the stored events - see CalendarOccurrences - which is what Orbit.Web's
        // grid does too.
        var daysTheirEventIsOn = events
            .Where(calendarEvent => calendarEvent.ServerId is not null)
            .GroupBy(calendarEvent => calendarEvent.ServerId!.Value)
            .ToDictionary(
                calendarEvent => calendarEvent.Key,
                calendarEvent => calendarEvent
                    .Select(occurrence => occurrence.Details.StartUtc.ToLocalTime().Date)
                    .ToHashSet());

        return [.. taskLists
            .SelectMany(taskList => taskList.Items
                .Where(item => item.DueDateUtc is not null)
                .Where(item => !IsAlreadyDrawnAsItsEvent(item, daysTheirEventIsOn))
                .Select(item => new CalendarDeadline(
                    taskList.LocalId, item.Id, taskList.Title, item.Description,
                    item.DueDateUtc!.Value.ToLocalTime().Date,
                    item.DueDateUtc!.Value.ToLocalTime().ToString("g", translations.DisplayCulture),
                    // An entry on a list its owner has closed is done, whatever its own tick says:
                    // marking a list finished with work still on it is a way of saying "no more of
                    // this" (Orbit.Core.Tasks.TaskList.IsMarkedCompleted), and the calendar would
                    // otherwise keep the deadlines it was closed to be rid of. The same rule Orbit.Web
                    // applies - see its Calendar.LoadDueTasksAsync.
                    // Finished with either way: a deadline somebody gave up on has stopped being owed.
                    item.IsCompleted || item.IsFailed || taskList.IsCompleted)
                {
                    Day = item.DueDateUtc!.Value.ToLocalTime().ToString("ddd d", translations.DisplayCulture),
                    Time = item.DueDateUtc!.Value.ToLocalTime().ToString("t", translations.DisplayCulture)
                }))
            .OrderBy(deadline => deadline.DueLocalDate)];
    }

    /// <inheritdoc cref="CalendarEventRow.Day"/>
    public string Day { get; init; } = string.Empty;

    /// <summary>
    /// What time it falls due, under the day. A deadline is *sorted* by its date alone - see
    /// <see cref="CalendarListEntry"/>, which explains why - but it has a time all the same, and the
    /// column that says when things happen would be half empty without it.
    /// </summary>
    public string Time { get; init; } = string.Empty;

    private static bool IsAlreadyDrawnAsItsEvent(
        TaskItemDto item, IReadOnlyDictionary<Guid, HashSet<DateTime>> daysTheirEventIsOn)
        => item.LinkedCalendarEventId is { } eventId
            && daysTheirEventIsOn.TryGetValue(eventId, out var days)
            && days.Contains(item.DueDateUtc!.Value.ToLocalTime().Date);
}
