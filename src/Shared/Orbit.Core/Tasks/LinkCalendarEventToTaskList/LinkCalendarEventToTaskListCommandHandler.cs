using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;

namespace Orbit.Core.Tasks.LinkCalendarEventToTaskList;

public sealed class LinkCalendarEventToTaskListCommandHandler
    : IRequestHandler<LinkCalendarEventToTaskListCommand, EditOutcome>
{
    private readonly TaskListAccessResolver _taskListAccessResolver;
    private readonly CalendarEventAccessResolver _calendarEventAccessResolver;
    private readonly ITaskRepository _taskRepository;

    public LinkCalendarEventToTaskListCommandHandler(
        TaskListAccessResolver taskListAccessResolver,
        CalendarEventAccessResolver calendarEventAccessResolver,
        ITaskRepository taskRepository)
    {
        _taskListAccessResolver = taskListAccessResolver;
        _calendarEventAccessResolver = calendarEventAccessResolver;
        _taskRepository = taskRepository;
    }

    /// <summary>
    /// Appends one entry rather than rewriting the list, which is the point of it being its own command:
    /// a client that had to read the list, add a row and send the whole thing back would overwrite
    /// whatever somebody else had changed in between, and would have to carry every field of every other
    /// entry correctly to avoid quietly dropping one.
    ///
    /// Adding the same event twice is not an error and does not add a second row: the list already says
    /// what it was asked to say.
    /// </summary>
    public async Task<EditOutcome> HandleAsync(
        LinkCalendarEventToTaskListCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskListAccessResolver.ResolveAsync(request.UserId, request.TaskListId, cancellationToken);
        if (taskList is null || !taskList.AccessLevel.AllowsEditing())
        {
            return EditOutcome.NotFound;
        }

        // A private list keeps no readable entries on the server - its items live sealed inside the
        // list, where only its owner's browser can add one. The same rule MoveTaskItem applies.
        if (taskList.IsPrivate)
        {
            throw new InvalidRequestException("An event can't be put on a private task list from here.");
        }

        if (taskList.IsLockedByAnotherUser(request.UserId, DateTimeOffset.UtcNow))
        {
            return EditOutcome.LockedBy(taskList.LockedByUserName!);
        }

        var calendarEvent = await _calendarEventAccessResolver.ResolveAsync(
            request.UserId, request.CalendarEventId, cancellationToken);
        if (calendarEvent is null)
        {
            return EditOutcome.NotFound;
        }

        if (taskList.Items.Any(item => item.LinkedCalendarEventId == request.CalendarEventId))
        {
            return EditOutcome.Success;
        }

        // No due date: the event says when it is, and a second answer here would put the same
        // appointment on the calendar twice - once as itself and once as something due.
        var reference = TaskItem.Create(
            calendarEvent.Details.Title,
            dueDateUtc: null,
            isCompleted: false,
            subject: new TaskItemSubject(TaskItemKind.Calendar, linkedCalendarEventId: calendarEvent.Id));

        // Everything else about the list is kept: putting an appointment on it says nothing about what
        // the list is called or how much it matters.
        taskList.Update(
            taskList.Title, [.. taskList.Items, reference], taskList.IsGroup, taskList.IsPrivate,
            taskList.EncryptedContent, taskList.Priority);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);
        return EditOutcome.Success;
    }
}
