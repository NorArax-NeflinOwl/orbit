using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.CreateTaskList;

public sealed class CreateTaskListCommandHandler : IRequestHandler<CreateTaskListCommand, Guid>
{
    private readonly ITaskRepository _taskRepository;

    public CreateTaskListCommandHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task<Guid> HandleAsync(CreateTaskListCommand request, CancellationToken cancellationToken)
    {
        var taskList = TaskList.Create(request.UserId, request.Title, request.Items);
        await _taskRepository.AddAsync(taskList, cancellationToken);
        return taskList.Id;
    }
}
