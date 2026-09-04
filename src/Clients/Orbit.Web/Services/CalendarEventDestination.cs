using Orbit.Contracts.Tasks;

namespace Orbit.Web.Services;

/// <summary>
/// Where pressing an event leads. An event a task list raised is opened as that list's entry rather
/// than as the event: the list is where the work is done, and the event form is a different thing from
/// ticking a step off. Everything else opens the event itself.
///
/// One rule in one place because an event is reached from three: the calendar's list, its grids, and
/// the dashboard's "Upcoming". Each of those used to answer differently, so where the same appointment
/// took you depended on which page you pressed it from.
/// </summary>
public static class CalendarEventDestination
{
    /// <summary>The list an event came from and the entry that made it, or null for an ordinary event.</summary>
    public sealed record Origin(TaskDto List, TaskItemDto Entry);

    /// <summary>
    /// Which list raised this event, if one did. Its own method because two questions are asked of the
    /// same lookup: where pressing it goes, and what to call it - the dashboard names a task list's
    /// deadline "Shopping: Milk" and had no way to name an *event* the same list raised, so the one
    /// entry on that card that came from somewhere lost where it came from.
    /// </summary>
    public static Origin? RaisedBy(Guid calendarEventId, IEnumerable<TaskDto>? taskLists)
    {
        foreach (var taskList in taskLists ?? [])
        {
            if (taskList.Items.FirstOrDefault(item => item.LinkedCalendarEventId == calendarEventId) is { } entry)
            {
                return new Origin(taskList, entry);
            }
        }

        return null;
    }

    public static string For(Guid calendarEventId, IEnumerable<TaskDto>? taskLists)
        => RaisedBy(calendarEventId, taskLists) is { } origin
            ? $"/tasks/{origin.List.Id}/items/{origin.Entry.Id}"
            : $"/calendar/{calendarEventId}";
}
