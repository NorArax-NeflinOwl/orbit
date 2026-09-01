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
    public static string For(Guid calendarEventId, IEnumerable<TaskDto>? taskLists)
    {
        foreach (var taskList in taskLists ?? [])
        {
            if (taskList.Items.FirstOrDefault(item => item.LinkedCalendarEventId == calendarEventId) is { } entry)
            {
                return $"/tasks/{taskList.Id}/items/{entry.Id}";
            }
        }

        return $"/calendar/{calendarEventId}";
    }
}
