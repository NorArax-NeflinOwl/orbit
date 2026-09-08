using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.MoveTaskItem;

public sealed class MoveTaskItemCommandHandler : IRequestHandler<MoveTaskItemCommand, EditOutcome>
{
    private readonly TaskListAccessResolver _taskListAccessResolver;
    private readonly ITaskRepository _taskRepository;
    private readonly TaskListLinkValidator _taskListLinkValidator;

    public MoveTaskItemCommandHandler(
        TaskListAccessResolver taskListAccessResolver, ITaskRepository taskRepository, TaskListLinkValidator taskListLinkValidator)
    {
        _taskListAccessResolver = taskListAccessResolver;
        _taskRepository = taskRepository;
        _taskListLinkValidator = taskListLinkValidator;
    }

    /// <summary>
    /// Moves one item out of its current list and into another, entirely - unlike LinkedTaskListId
    /// (which just mirrors another list's completion state while the item stays where it is), this
    /// changes which list the item actually belongs to. Both lists must resolve to CanEdit for the
    /// caller and belong to the same owner - moving an item into a list owned by someone else isn't
    /// supported, mirroring how TaskListLinkValidator already scopes links to "the same user"'s lists.
    /// </summary>
    public async Task<EditOutcome> HandleAsync(MoveTaskItemCommand request, CancellationToken cancellationToken)
    {
        if (request.SourceTaskListId == request.TargetTaskListId)
        {
            return EditOutcome.NotFound;
        }

        var sourceList = await _taskListAccessResolver.ResolveAsync(request.UserId, request.SourceTaskListId, cancellationToken);
        if (sourceList is null || !sourceList.AccessLevel.AllowsEditing())
        {
            return EditOutcome.NotFound;
        }

        var targetList = await _taskListAccessResolver.ResolveAsync(request.UserId, request.TargetTaskListId, cancellationToken);
        if (targetList is null || !targetList.AccessLevel.AllowsEditing())
        {
            return EditOutcome.NotFound;
        }

        if (targetList.UserId != sourceList.UserId)
        {
            // Two lists with different owners, which for a reader who can edit both means one of them
            // was shared with them. An entry belongs to its list and travels with it, so moving one out
            // would take it away from everybody else the list was shared with - and moving one in would
            // hand them something they never agreed to.
            //
            // Refused rather than NotFound, which is what this used to answer: both lists are on the
            // reader's own screen, so "no such list" sends them looking for a mistake they did not
            // make. The reason names the way round it can be done - a copy, which is theirs and leaves
            // the shared list alone. See CopyTaskItemCommand.
            return EditOutcome.RefusedBecause(
                "This entry is shared along with the whole list it is on, so it can't be moved out of it. Copy it instead.");
        }

        if (sourceList.IsPrivate || targetList.IsPrivate)
        {
            // A private list keeps no readable items on the server, so it has nothing to move out of and
            // nowhere to put anything moved in: without this the item would be taken off the source and
            // then dropped when the target sealed itself again. Moving between private lists is a client
            // job - it can read both, and saving each one re-seals it.
            throw new InvalidRequestException("Items can't be moved into or out of a private task list.");
        }

        var nowUtc = DateTimeOffset.UtcNow;
        if (sourceList.IsLockedByAnotherUser(request.UserId, nowUtc))
        {
            return EditOutcome.LockedBy(sourceList.LockedByUserName!);
        }
        if (targetList.IsLockedByAnotherUser(request.UserId, nowUtc))
        {
            return EditOutcome.LockedBy(targetList.LockedByUserName!);
        }

        var item = sourceList.Items.FirstOrDefault(candidate => candidate.Id == request.TaskItemId);
        if (item is null)
        {
            return EditOutcome.NotFound;
        }

        var updatedSourceItems = sourceList.Items.Where(candidate => candidate.Id != request.TaskItemId).ToList();
        var updatedTargetItems = targetList.Items.Append(item).ToList();

        // A moved item could theoretically already carry a LinkedTaskListId pointing at the list it's
        // moving into, which TaskListLinkValidator would reject as a self-link.
        await _taskListLinkValidator.ValidateAsync(targetList.UserId, targetList.Id, updatedTargetItems, cancellationToken);

        // Both lists keep everything about themselves but their items: moving one entry says nothing
        // about how much either list matters.
        sourceList.Update(
            sourceList.Title, updatedSourceItems, sourceList.IsGroup, sourceList.IsPrivate,
            sourceList.EncryptedContent, sourceList.Priority);
        targetList.Update(
            targetList.Title, updatedTargetItems, targetList.IsGroup, targetList.IsPrivate,
            targetList.EncryptedContent, targetList.Priority);
        await _taskRepository.UpdateManyAsync([sourceList, targetList], cancellationToken);
        return EditOutcome.Success;
    }
}
