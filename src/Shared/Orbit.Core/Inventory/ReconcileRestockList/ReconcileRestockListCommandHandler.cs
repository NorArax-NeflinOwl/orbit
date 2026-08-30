using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;

namespace Orbit.Core.Inventory.ReconcileRestockList;

public sealed class ReconcileRestockListCommandHandler : IRequestHandler<ReconcileRestockListCommand, RestockOutcome>
{
    private readonly TaskListAccessResolver _taskListAccessResolver;
    private readonly RestockCompletion _restockCompletion;

    public ReconcileRestockListCommandHandler(
        TaskListAccessResolver taskListAccessResolver, RestockCompletion restockCompletion)
    {
        _taskListAccessResolver = taskListAccessResolver;
        _restockCompletion = restockCompletion;
    }

    /// <summary>
    /// Nothing at all for a list the caller cannot edit, or one no warehouse tracks. Settling changes the
    /// list and the shelf, so it needs the same standing an ordinary edit does - a reader holding
    /// read-only access can look at the crossed-off errands without being the one who clears them.
    /// </summary>
    public async Task<RestockOutcome> HandleAsync(ReconcileRestockListCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskListAccessResolver.ResolveAsync(request.UserId, request.TaskListId, cancellationToken);
        if (taskList is null || !taskList.AccessLevel.AllowsEditing())
        {
            return RestockOutcome.Nothing;
        }

        return await _restockCompletion.ReconcileAsync(request.TaskListId, cancellationToken);
    }
}
