using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.GetTaskLists;

public sealed class GetTaskListsQueryHandler : IRequestHandler<GetTaskListsQuery, IReadOnlyList<TaskList>>
{
    private readonly ITaskRepository _taskRepository;

    public GetTaskListsQueryHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public Task<IReadOnlyList<TaskList>> HandleAsync(GetTaskListsQuery request, CancellationToken cancellationToken)
        => _taskRepository.GetAllAsync(request.UserId, cancellationToken);
}
