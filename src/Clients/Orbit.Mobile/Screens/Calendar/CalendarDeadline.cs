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
    /// </summary>
    public static IReadOnlyList<CalendarDeadline> From(
        IReadOnlyList<LocalTaskList> taskLists, Translations translations)
        => [.. taskLists
            .SelectMany(taskList => taskList.Items
                .Where(item => item.DueDateUtc is not null)
                .Select(item => new CalendarDeadline(
                    taskList.LocalId, taskList.Title, item.Description,
                    item.DueDateUtc!.Value.ToLocalTime().Date,
                    item.DueDateUtc!.Value.ToLocalTime().ToString("g", translations.DisplayCulture),
                    item.IsCompleted)))
            .OrderBy(deadline => deadline.DueLocalDate)];
}
