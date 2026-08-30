using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;

namespace Orbit.Core.Inventory.FinishRestocking;

/// <summary>
/// The answer to being asked, on crossing off "Update stock levels", whether the rest of the list is
/// done too. Doing it one errand at a time is the other path (see RestockCompletion, which the ordinary
/// task save calls); this is for the reader who went and restocked everything at once.
/// </summary>
public sealed class FinishRestockingCommandHandler : IRequestHandler<FinishRestockingCommand, int>
{
    private readonly ITaskRepository _taskRepository;
    private readonly RestockCompletion _restockCompletion;

    public FinishRestockingCommandHandler(ITaskRepository taskRepository, RestockCompletion restockCompletion)
    {
        _taskRepository = taskRepository;
        _restockCompletion = restockCompletion;
    }

    public async Task<int> HandleAsync(FinishRestockingCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskRepository.GetByIdAsync(request.UserId, request.TaskListId, cancellationToken);
        if (taskList is null)
        {
            return 0;
        }

        var toppedUp = await _restockCompletion.TopUpEverythingAsync(request.TaskListId, cancellationToken);

        // The whole list, the standing reminder included: the question asked was whether to finish the
        // task, and the reminder is brought back tomorrow by RemindDaily rather than by being left open.
        if (taskList.CompleteEverything())
        {
            await _taskRepository.UpdateAsync(taskList, cancellationToken);
        }

        // And then the finished errands leave, the same way they do when they are crossed off one at a
        // time - nothing on this list is missing any more, so nothing on it should still be asking for
        // it. The top-ups above have already happened, so this run only has the entries left to settle.
        await _restockCompletion.ReconcileAsync(request.TaskListId, cancellationToken);

        return toppedUp;
    }
}
