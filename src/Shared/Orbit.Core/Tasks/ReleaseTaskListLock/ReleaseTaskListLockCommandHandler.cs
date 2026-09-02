using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.ReleaseTaskListLock;

public sealed class ReleaseTaskListLockCommandHandler : IRequestHandler<ReleaseTaskListLockCommand, bool>
{
    private readonly TaskListAccessResolver _taskListAccessResolver;
    private readonly ITaskRepository _taskRepository;

    public ReleaseTaskListLockCommandHandler(TaskListAccessResolver taskListAccessResolver, ITaskRepository taskRepository)
    {
        _taskListAccessResolver = taskListAccessResolver;
        _taskRepository = taskRepository;
    }

    public async Task<bool> HandleAsync(ReleaseTaskListLockCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskListAccessResolver.ResolveAsync(request.UserId, request.TaskListId, cancellationToken);
        if (taskList is null)
        {
            return false;
        }

        taskList.ReleaseLock(request.UserId);
        await _taskRepository.UpdateLockAsync(taskList, cancellationToken);
        return true;
    }
}
