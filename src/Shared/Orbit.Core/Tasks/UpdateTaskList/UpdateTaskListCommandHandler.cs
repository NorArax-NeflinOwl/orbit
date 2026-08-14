using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.UpdateTaskList;

public sealed class UpdateTaskListCommandHandler : IRequestHandler<UpdateTaskListCommand, bool>
{
    private readonly ITaskRepository _taskRepository;

    public UpdateTaskListCommandHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    /// <summary>
    /// Returns false instead of throwing when the task list is missing or not owned by the requesting
    /// user, so the API can turn that into a 404 either way, without leaking which is the case.
    /// </summary>
    public async Task<bool> HandleAsync(UpdateTaskListCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken);
        if (taskList is null)
        {
            return false;
        }

        taskList.Update(request.Title, request.Items);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);
        return true;
    }
}
