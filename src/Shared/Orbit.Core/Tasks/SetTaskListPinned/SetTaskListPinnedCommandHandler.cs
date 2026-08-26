using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.SetTaskListPinned;

/// <summary>
/// Only the list's owner can pin it. Pinning is about where a card sits on one person's own page, so a
/// recipient pinning a shared list would be moving it for its owner instead of for themselves - a
/// per-reader pin is a different feature, and a worse one to arrive at by accident.
/// </summary>
public sealed class SetTaskListPinnedCommandHandler : IRequestHandler<SetTaskListPinnedCommand, bool>
{
    private readonly ITaskRepository _taskRepository;

    public SetTaskListPinnedCommandHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task<bool> HandleAsync(SetTaskListPinnedCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskRepository.GetByIdAsync(request.UserId, request.TaskListId, cancellationToken);
        if (taskList is null || taskList.UserId != request.UserId)
        {
            return false;
        }

        taskList.SetPinned(request.IsPinned);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);
        return true;
    }
}
