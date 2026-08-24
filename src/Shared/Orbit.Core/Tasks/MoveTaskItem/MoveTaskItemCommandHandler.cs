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
        if (sourceList is null || sourceList.AccessLevel != ShareAccessLevel.CanEdit)
        {
            return EditOutcome.NotFound;
        }

        var targetList = await _taskListAccessResolver.ResolveAsync(request.UserId, request.TargetTaskListId, cancellationToken);
        if (targetList is null || targetList.AccessLevel != ShareAccessLevel.CanEdit || targetList.UserId != sourceList.UserId)
        {
            return EditOutcome.NotFound;
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

        sourceList.Update(sourceList.Title, updatedSourceItems);
        targetList.Update(targetList.Title, updatedTargetItems);
        await _taskRepository.UpdateManyAsync([sourceList, targetList], cancellationToken);
        return EditOutcome.Success;
    }
}
