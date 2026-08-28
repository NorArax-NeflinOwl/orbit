using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.CreateTaskList;

public sealed class CreateTaskListCommandHandler : IRequestHandler<CreateTaskListCommand, Guid>
{
    private readonly ITaskRepository _taskRepository;
    private readonly TaskListLinkValidator _taskListLinkValidator;

    public CreateTaskListCommandHandler(ITaskRepository taskRepository, TaskListLinkValidator taskListLinkValidator)
    {
        _taskRepository = taskRepository;
        _taskListLinkValidator = taskListLinkValidator;
    }

    public async Task<Guid> HandleAsync(CreateTaskListCommand request, CancellationToken cancellationToken)
    {
        await _taskListLinkValidator.ValidateAsync(request.UserId, taskListId: null, request.Items, cancellationToken);

        var taskList = TaskList.Create(
            request.UserId, request.Title, request.Items, request.IsGroup, request.IsPrivate, request.EncryptedContent,
            request.Priority, isPinned: false, request.Kind, request.Location);
        await _taskRepository.AddAsync(taskList, cancellationToken);
        return taskList.Id;
    }
}
