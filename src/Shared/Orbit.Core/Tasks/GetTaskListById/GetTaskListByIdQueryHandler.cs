using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.GetTaskListById;

public sealed class GetTaskListByIdQueryHandler : IRequestHandler<GetTaskListByIdQuery, TaskList?>
{
    private readonly TaskListAccessResolver _taskListAccessResolver;
    private readonly LinkedTaskCompletionResolver _linkedTaskCompletionResolver;

    public GetTaskListByIdQueryHandler(TaskListAccessResolver taskListAccessResolver, LinkedTaskCompletionResolver linkedTaskCompletionResolver)
    {
        _taskListAccessResolver = taskListAccessResolver;
        _linkedTaskCompletionResolver = linkedTaskCompletionResolver;
    }

    /// <summary>
    /// Fetches every task list the user has access to (owned or shared), rather than just the requested
    /// one, because resolving a linked item's completion (see <see cref="LinkedTaskCompletionResolver"/>)
    /// requires being able to follow its link to whichever other list it points at - though that link
    /// only ever resolves within a *single owner's* lists (see TaskListLinkValidator), so a linked item
    /// on a list shared with this caller still falls back to "not completed" if the link points at a
    /// list only the owner (not this caller) has access to.
    /// </summary>
    public async Task<TaskList?> HandleAsync(GetTaskListByIdQuery request, CancellationToken cancellationToken)
    {
        var accessibleTaskLists = await _taskListAccessResolver.ResolveAllAsync(request.UserId, cancellationToken);
        var resolvedTaskLists = _linkedTaskCompletionResolver.ResolveAll(accessibleTaskLists);
        return resolvedTaskLists.FirstOrDefault(taskList => taskList.Id == request.Id);
    }
}
