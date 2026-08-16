using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.GetTaskListById;

public sealed class GetTaskListByIdQueryHandler : IRequestHandler<GetTaskListByIdQuery, TaskList?>
{
    private readonly ITaskRepository _taskRepository;
    private readonly LinkedTaskCompletionResolver _linkedTaskCompletionResolver;

    public GetTaskListByIdQueryHandler(ITaskRepository taskRepository, LinkedTaskCompletionResolver linkedTaskCompletionResolver)
    {
        _taskRepository = taskRepository;
        _linkedTaskCompletionResolver = linkedTaskCompletionResolver;
    }

    /// <summary>
    /// Fetches every task list the user owns, rather than just the requested one, because resolving a
    /// linked item's completion (see <see cref="LinkedTaskCompletionResolver"/>) requires being able to
    /// follow its link to whichever other list it points at.
    /// </summary>
    public async Task<TaskList?> HandleAsync(GetTaskListByIdQuery request, CancellationToken cancellationToken)
    {
        var allTaskLists = await _taskRepository.GetAllAsync(request.UserId, cancellationToken);
        var resolvedTaskLists = _linkedTaskCompletionResolver.ResolveAll(allTaskLists);
        return resolvedTaskLists.FirstOrDefault(taskList => taskList.Id == request.Id);
    }
}
