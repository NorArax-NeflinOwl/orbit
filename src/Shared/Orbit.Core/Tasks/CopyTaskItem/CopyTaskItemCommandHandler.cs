using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.CopyTaskItem;

/// <summary>
/// The reader has to be able to *read* the entry and to *edit* the list it is going onto - which is a
/// weaker pair of rules than a move needs, and deliberately so: nothing about the list it came from
/// changes, so a list shared read-only can still be copied out of.
/// </summary>
public sealed class CopyTaskItemCommandHandler : IRequestHandler<CopyTaskItemCommand, EditOutcome>
{
    private readonly TaskListAccessResolver _taskListAccessResolver;
    private readonly ITaskRepository _taskRepository;

    public CopyTaskItemCommandHandler(TaskListAccessResolver taskListAccessResolver, ITaskRepository taskRepository)
    {
        _taskListAccessResolver = taskListAccessResolver;
        _taskRepository = taskRepository;
    }

    public async Task<EditOutcome> HandleAsync(CopyTaskItemCommand request, CancellationToken cancellationToken)
    {
        if (request.SourceTaskListId == request.TargetTaskListId)
        {
            return EditOutcome.NotFound;
        }

        var sourceList = await _taskListAccessResolver.ResolveAsync(request.UserId, request.SourceTaskListId, cancellationToken);
        if (sourceList is null)
        {
            return EditOutcome.NotFound;
        }

        var targetList = await _taskListAccessResolver.ResolveAsync(request.UserId, request.TargetTaskListId, cancellationToken);
        if (targetList is null || !targetList.AccessLevel.AllowsEditing())
        {
            return EditOutcome.NotFound;
        }

        if (sourceList.IsPrivate || targetList.IsPrivate)
        {
            // The same reason a move refuses it: a private list keeps nothing readable on the server, so
            // there is nothing here to copy from and nowhere to put anything copied in. Between two
            // private lists it is the client's job - it can read both, and saving each re-seals it.
            return EditOutcome.RefusedBecause("Entries can't be copied into or out of a private task list.");
        }

        if (targetList.IsLockedByAnotherUser(request.UserId, DateTimeOffset.UtcNow))
        {
            return EditOutcome.LockedBy(targetList.LockedByUserName!);
        }

        var item = sourceList.Items.FirstOrDefault(candidate => candidate.Id == request.TaskItemId);
        if (item is null)
        {
            return EditOutcome.NotFound;
        }

        targetList.Update(
            targetList.Title, [.. targetList.Items, CopyOf(item)], targetList.IsGroup, targetList.IsPrivate,
            targetList.EncryptedContent, targetList.Priority);
        await _taskRepository.UpdateAsync(targetList, cancellationToken);
        return EditOutcome.Success;
    }

    /// <summary>
    /// The same entry again, under a new id: what it is called, what it says, when it is due, whether it
    /// is done, what it is filed under, what it asks for, and when it speaks up.
    ///
    /// What a copy deliberately does not carry is the three things that are references rather than
    /// fields - the appointment, the shelf item, and the lists the entry stands for. Two entries
    /// pointing at one appointment is the drift this codebase avoids everywhere else, and after a copy
    /// across a share those rows belong to the other account anyway: the reader would be given a link
    /// to an appointment they cannot open. What the entry says about the place is text and does come
    /// across, so a copied appointment still says where it was.
    /// </summary>
    private static TaskItem CopyOf(TaskItem item)
        => TaskItem.Create(
            item.Description,
            item.DueDateUtc,
            item.IsCompleted,
            linkedTaskListIds: null,
            item.Reminders,
            new TaskItemSubject(item.Kind, item.Location),
            item.Categories,
            item.Product,
            item.Notes,
            item.IsFailed,
            // The ways it can be got done are words the entry says, so they travel; one of them that is
            // another list is a reference, and a copy carries none of those. So does how much of its
            // product it needs, which is the entry's own answer rather than a pointer at anything.
            alternatives: [.. item.Alternatives.Where(way => !way.IsAList)],
            requiredQuantity: item.RequiredQuantity);
}
