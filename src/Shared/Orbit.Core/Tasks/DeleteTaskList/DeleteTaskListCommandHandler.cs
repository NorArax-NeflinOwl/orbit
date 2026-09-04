using Orbit.Core.Abstractions;
using Orbit.Core.Sync;

namespace Orbit.Core.Tasks.DeleteTaskList;

public sealed class DeleteTaskListCommandHandler : IRequestHandler<DeleteTaskListCommand, bool>
{
    private readonly ITaskRepository _taskRepository;
    private readonly ITaskListShareRepository _taskListShareRepository;
    private readonly ISyncTombstoneRepository _syncTombstoneRepository;

    public DeleteTaskListCommandHandler(
        ITaskRepository taskRepository, ITaskListShareRepository taskListShareRepository,
        ISyncTombstoneRepository syncTombstoneRepository)
    {
        _taskRepository = taskRepository;
        _taskListShareRepository = taskListShareRepository;
        _syncTombstoneRepository = syncTombstoneRepository;
    }

    /// <summary>
    /// Deletes the caller's own task list, or - when it is somebody else's, shared with them - takes it
    /// off their list by dropping the grant. False when it is neither, so the API answers 404 without
    /// leaking which of the two it was.
    ///
    /// By default the lists a group list gathers are left alone, and other task lists linking an item to
    /// this one are left with a dangling <see cref="TaskItem.LinkedTaskListIds"/> -
    /// <see cref="LinkedTaskCompletionResolver"/> already treats a link to a missing list as "not
    /// completed" rather than failing, so this is safe. When the caller has said the gathered lists
    /// should go too, <see cref="EverythingGatheredByAsync"/> works out which ones that is.
    /// </summary>
    public async Task<bool> HandleAsync(DeleteTaskListCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken);
        if (taskList is null)
        {
            // Not the owner's. A recipient asking to be rid of something shared with them means
            // taking it off their own list - destroying somebody else's task list is not theirs to
            // do. Removing the accepted grant does exactly that and leaves the owner's untouched.
            if (await _taskListShareRepository.FindAcceptedGrantAsync(request.Id, request.UserId, cancellationToken) is null)
            {
                return false;
            }

            await _taskListShareRepository.RemoveAcceptedGrantAsync(request.Id, request.UserId, cancellationToken);
            await RecordTombstoneAsync(request.UserId, request.Id, cancellationToken);
            return true;
        }

        // Worked out before anything is deleted: once the group list is gone there is nothing left
        // saying what it gathered.
        var gathered = request.DeleteTheListsItGathers
            ? await EverythingGatheredByAsync(request.UserId, taskList, cancellationToken)
            : [];

        await _taskRepository.DeleteAsync(request.UserId, request.Id, cancellationToken);
        await RecordTombstoneAsync(request.UserId, request.Id, cancellationToken);

        foreach (var gatheredId in gathered)
        {
            await _taskRepository.DeleteAsync(request.UserId, gatheredId, cancellationToken);
            await RecordTombstoneAsync(request.UserId, gatheredId, cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Every list this one stands for, and every list those stand for in turn - a group list may gather
    /// another group list, and deleting the top of a tree while leaving its middle behind would answer
    /// "delete these too" with "some of them".
    ///
    /// Two things it will not do. It never leaves the caller's own lists: a link may point at somebody
    /// else's list shared with them, and deleting that is not theirs to do (the repository would refuse
    /// anyway, but asking for it would still be asking). And it cannot loop - a list gathering something
    /// that gathers it back is refused when it is made (see TaskListLinkValidator), but a tree read
    /// without a visited set would hang here on any that slipped through.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> EverythingGatheredByAsync(
        Guid userId, TaskList root, CancellationToken cancellationToken)
    {
        var found = new List<Guid>();
        var seen = new HashSet<Guid> { root.Id };
        var toRead = new Queue<TaskList>();
        toRead.Enqueue(root);

        while (toRead.Count > 0)
        {
            var gatherer = toRead.Dequeue();
            foreach (var gatheredId in gatherer.Items.SelectMany(item => item.LinkedTaskListIds))
            {
                if (!seen.Add(gatheredId))
                {
                    continue;
                }

                if (await _taskRepository.GetByIdAsync(userId, gatheredId, cancellationToken) is not { } gathered)
                {
                    continue;
                }

                found.Add(gatheredId);
                toRead.Enqueue(gathered);
            }
        }

        return found;
    }

    /// <summary>
    /// Tombstones are per-user, which is what lets a dropped grant leave one: the list is gone
    /// from this reader's list and from nobody else's, and that is exactly what their next delta
    /// needs to say.
    /// </summary>
    private Task RecordTombstoneAsync(Guid userId, Guid taskListId, CancellationToken cancellationToken)
        => _syncTombstoneRepository.RecordAsync(
            new SyncTombstone(userId, SyncEntityType.TaskList, taskListId, DateTimeOffset.UtcNow),
            cancellationToken);
}
