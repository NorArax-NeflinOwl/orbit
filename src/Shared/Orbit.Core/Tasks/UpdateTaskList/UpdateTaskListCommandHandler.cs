using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.UpdateTaskList;

public sealed class UpdateTaskListCommandHandler : IRequestHandler<UpdateTaskListCommand, bool>
{
    private readonly ITaskRepository _taskRepository;
    private readonly TaskListLinkValidator _taskListLinkValidator;

    public UpdateTaskListCommandHandler(ITaskRepository taskRepository, TaskListLinkValidator taskListLinkValidator)
    {
        _taskRepository = taskRepository;
        _taskListLinkValidator = taskListLinkValidator;
    }

    /// <summary>
    /// Returns false instead of throwing when the task list is missing, not owned by the requesting
    /// user, or is a read-only shared copy, so the API can turn any of those into a 404, without
    /// leaking which one applies.
    /// </summary>
    public async Task<bool> HandleAsync(UpdateTaskListCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken);
        if (taskList is null || (taskList.IsShared && taskList.AccessLevel == ShareAccessLevel.ReadOnly))
        {
            return false;
        }

        await _taskListLinkValidator.ValidateAsync(request.UserId, request.Id, request.Items, cancellationToken);

        taskList.Update(request.Title, request.Items);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);
        return true;
    }
}
