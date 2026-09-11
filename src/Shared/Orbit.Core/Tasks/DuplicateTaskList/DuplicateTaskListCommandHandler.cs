using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.DuplicateTaskList;

public sealed class DuplicateTaskListCommandHandler : IRequestHandler<DuplicateTaskListCommand, Guid?>
{
    private readonly ITaskRepository _taskRepository;

    public DuplicateTaskListCommandHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    /// <summary>
    /// The list, its entries and what each of them is about. Three things are deliberately left behind:
    ///
    /// <list type="bullet">
    ///   <item>the <b>pin</b>, which says where a card sits on this reader's page - two cards cannot both
    ///     be the one being kept in front of them;</item>
    ///   <item>whether the reader had said the list is <b>finished</b>, since they have not said it about
    ///     this one. Its entries carry their own ticks, so a copy of a done list still reads as done -
    ///     that answer came from the entries and travels with them;</item>
    ///   <item>the <b>appointment</b> an entry stands for. An event is raised by exactly one entry (see
    ///     CalendarEventDestination.RaisedBy, which returns the first it finds), so a second entry
    ///     pointing at the same one would make which of them owns it a matter of iteration order. The
    ///     entry is copied as ordinary work, keeping the place written on it where it had one.</item>
    /// </list>
    ///
    /// A sealed list is copied as it stands: its entries are inside a payload the server cannot open,
    /// and the copy opens with the same key because it has the same owner.
    /// </summary>
    public async Task<Guid?> HandleAsync(DuplicateTaskListCommand request, CancellationToken cancellationToken)
    {
        if (await _taskRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken) is not { } taskList)
        {
            return null;
        }

        var copy = TaskList.Create(
            taskList.UserId,
            // A sealed list's title is inside the payload, so there is nothing here to rename.
            taskList.IsPrivate ? taskList.Title : request.Name ?? taskList.Title,
            [.. taskList.Items.Select(CopyOf)],
            taskList.IsGroup,
            taskList.IsPrivate,
            taskList.EncryptedContent,
            taskList.Priority,
            isPinned: false,
            taskList.Description,
            taskList.FolderId);
        await _taskRepository.AddAsync(copy, cancellationToken);
        return copy.Id;
    }

    /// <summary>
    /// One entry, with a new identity and everything else it said. Links to other lists are kept - a
    /// group list that gathers the same members is what a copy of it is - and the link to a calendar
    /// event is not, for the reason the class comment gives.
    /// </summary>
    private static TaskItem CopyOf(TaskItem item)
        => TaskItem.Create(
            item.Description,
            item.DueDateUtc,
            item.IsCompleted,
            item.LinkedTaskListIds,
            item.Reminders,
            new TaskItemSubject(item.Subject.Kind, item.Subject.Location, linkedCalendarEventId: null, item.Subject.LinkedInventoryItemId),
            item.Categories,
            item.Product,
            item.Notes,
            item.IsFailed,
            // The ways it is done by, for the reason the links are kept: they are what the entry is.
            alternatives: item.Alternatives);
}
