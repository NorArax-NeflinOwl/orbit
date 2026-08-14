using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.GetTaskListById;

public sealed class GetTaskListByIdQueryHandler : IRequestHandler<GetTaskListByIdQuery, TaskList?>
{
    private readonly ITaskRepository _taskRepository;

    public GetTaskListByIdQueryHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public Task<TaskList?> HandleAsync(GetTaskListByIdQuery request, CancellationToken cancellationToken)
        => _taskRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken);
}
