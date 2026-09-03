namespace Orbit.Contracts.Tasks;

/// <summary>
/// Which event to put on a task list as an entry pointing at it - see
/// Orbit.Core.Tasks.LinkCalendarEventToTaskList.LinkCalendarEventToTaskListCommand. Nothing about the
/// event travels with this but its id: the entry references it rather than copying it.
/// </summary>
public sealed record LinkCalendarEventRequest(Guid CalendarEventId);
