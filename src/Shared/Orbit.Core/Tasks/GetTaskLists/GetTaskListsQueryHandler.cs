using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.GetTaskLists;

public sealed class GetTaskListsQueryHandler : IRequestHandler<GetTaskListsQuery, IReadOnlyList<TaskList>>
{
    private readonly TaskListAccessResolver _taskListAccessResolver;
    private readonly LinkedTaskCompletionResolver _linkedTaskCompletionResolver;

    public GetTaskListsQueryHandler(TaskListAccessResolver taskListAccessResolver, LinkedTaskCompletionResolver linkedTaskCompletionResolver)
    {
        _taskListAccessResolver = taskListAccessResolver;
        _linkedTaskCompletionResolver = linkedTaskCompletionResolver;
    }

    public async Task<IReadOnlyList<TaskList>> HandleAsync(GetTaskListsQuery request, CancellationToken cancellationToken)
    {
        var taskLists = await _taskListAccessResolver.ResolveAllAsync(request.UserId, request.UpdatedSinceUtc, cancellationToken);
        return _linkedTaskCompletionResolver.ResolveAll(taskLists);
    }
}
