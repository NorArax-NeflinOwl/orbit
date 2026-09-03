using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.LinkCalendarEventToTaskList;

/// <summary>
/// Puts an event on a task list as an entry that points at it - the same appointment reached from the
/// list, rather than a second copy of it living there.
///
/// The other direction of what a Calendar entry already does: a task list entry of that kind creates
/// the event when the list is saved (see UpdateTaskListCommandHandler and the web's TaskEditor). This
/// is the same relationship written from the calendar's end, for an event that exists already.
///
/// Nothing about the event is copied onto the entry but its title, which is what the row is read by.
/// Everything else - when it is, where it is, who is coming - stays on the event and is read back
/// through <see cref="TaskItem.LinkedCalendarEventId"/>, so the two can never come to disagree.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record LinkCalendarEventToTaskListCommand(Guid UserId, Guid TaskListId, Guid CalendarEventId)
    : IRequest<EditOutcome>;
