using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.SetTaskListPinned;

/// <inheritdoc cref="Orbit.Core.Notes.SetNotePinned.SetNotePinnedCommandHandler"/>
public sealed class SetTaskListPinnedCommandHandler : IRequestHandler<SetTaskListPinnedCommand, bool>
{
    private readonly ITaskRepository _taskRepository;
    private readonly ITaskListShareRepository _taskListShareRepository;

    public SetTaskListPinnedCommandHandler(
        ITaskRepository taskRepository, ITaskListShareRepository taskListShareRepository)
    {
        _taskRepository = taskRepository;
        _taskListShareRepository = taskListShareRepository;
    }

    public async Task<bool> HandleAsync(SetTaskListPinnedCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskRepository.GetByIdAsync(request.UserId, request.TaskListId, cancellationToken);
        if (taskList is not null && taskList.UserId == request.UserId)
        {
            taskList.SetPinned(request.IsPinned);
            await _taskRepository.UpdateAsync(taskList, cancellationToken);
            return true;
        }

        var grant = await _taskListShareRepository.FindAcceptedGrantAsync(
            request.TaskListId, request.UserId, cancellationToken);
        if (grant is null)
        {
            return false;
        }

        if (grant.SetPinnedByRecipient(request.IsPinned))
        {
            await _taskListShareRepository.UpdateAsync(grant, cancellationToken);
        }

        return true;
    }
}
