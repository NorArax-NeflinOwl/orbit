using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.GetTaskLists;

public sealed class GetTaskListsQueryHandler : IRequestHandler<GetTaskListsQuery, IReadOnlyList<TaskList>>
{
    private readonly ITaskRepository _taskRepository;
    private readonly LinkedTaskCompletionResolver _linkedTaskCompletionResolver;

    public GetTaskListsQueryHandler(ITaskRepository taskRepository, LinkedTaskCompletionResolver linkedTaskCompletionResolver)
    {
        _taskRepository = taskRepository;
        _linkedTaskCompletionResolver = linkedTaskCompletionResolver;
    }

    public async Task<IReadOnlyList<TaskList>> HandleAsync(GetTaskListsQuery request, CancellationToken cancellationToken)
    {
        var taskLists = await _taskRepository.GetAllAsync(request.UserId, cancellationToken);
        return _linkedTaskCompletionResolver.ResolveAll(taskLists);
    }
}
