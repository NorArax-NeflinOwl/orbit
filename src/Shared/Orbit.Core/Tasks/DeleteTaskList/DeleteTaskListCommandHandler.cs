using Orbit.Core.Abstractions;
using Orbit.Core.Sync;

namespace Orbit.Core.Tasks.DeleteTaskList;

public sealed class DeleteTaskListCommandHandler : IRequestHandler<DeleteTaskListCommand, bool>
{
    private readonly ITaskRepository _taskRepository;
    private readonly ISyncTombstoneRepository _syncTombstoneRepository;

    public DeleteTaskListCommandHandler(ITaskRepository taskRepository, ISyncTombstoneRepository syncTombstoneRepository)
    {
        _taskRepository = taskRepository;
        _syncTombstoneRepository = syncTombstoneRepository;
    }

    /// <summary>
    /// Returns false instead of throwing when the task list is missing or not owned by the requesting
    /// user, so the API can turn that into a 404 either way, without leaking which is the case. Other
    /// task lists that link one of their items to this one are left with a dangling
    /// <see cref="TaskItem.LinkedTaskListId"/> - <see cref="LinkedTaskCompletionResolver"/> already
    /// treats a link to a missing list as "not completed" rather than failing, so this is safe.
    /// </summary>
    public async Task<bool> HandleAsync(DeleteTaskListCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken);
        if (taskList is null)
        {
            return false;
        }

        await _taskRepository.DeleteAsync(request.UserId, request.Id, cancellationToken);
        await _syncTombstoneRepository.RecordAsync(
            new SyncTombstone(request.UserId, SyncEntityType.TaskList, request.Id, DateTimeOffset.UtcNow), cancellationToken);
        return true;
    }
}
