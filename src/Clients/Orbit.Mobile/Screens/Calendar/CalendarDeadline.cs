using Orbit.Contracts.Tasks;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Calendar;

/// <summary>
/// A task entry falling due on a day, shown on the calendar beside the events. A deadline is as much a
/// thing happening on a day as an appointment is, and the phone's calendar showed only the appointments
/// - so a week with three things due looked empty. Orbit.Web's calendar has shown both all along.
/// </summary>
/// <param name="TaskListLocalId">Which list to open, since a deadline is read on the list it sits on.</param>
/// <param name="When">Already in the reader's calendar, so the row itself needs no dictionary.</param>
public sealed record CalendarDeadline(
    Guid TaskListLocalId, string ListTitle, string Description, DateTime DueLocalDate, string When,
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
        // Asked of the day the event is stored under, which on the phone is every day it is drawn on:
        // the calendar here shows an event once, at its own start, rather than on each day a repeat
        // lands on. Orbit.Web asks the occurrence, because its grid expands repeats.
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
                    taskList.LocalId, taskList.Title, item.Description,
                    item.DueDateUtc!.Value.ToLocalTime().Date,
                    item.DueDateUtc!.Value.ToLocalTime().ToString("g", translations.DisplayCulture),
                    item.IsCompleted)))
            .OrderBy(deadline => deadline.DueLocalDate)];
    }

    private static bool IsAlreadyDrawnAsItsEvent(
        TaskItemDto item, IReadOnlyDictionary<Guid, HashSet<DateTime>> daysTheirEventIsOn)
        => item.LinkedCalendarEventId is { } eventId
            && daysTheirEventIsOn.TryGetValue(eventId, out var days)
            && days.Contains(item.DueDateUtc!.Value.ToLocalTime().Date);
}
