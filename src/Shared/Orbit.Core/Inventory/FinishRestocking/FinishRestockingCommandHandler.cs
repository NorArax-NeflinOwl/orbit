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

        // Only the errands. The standing reminder is crossed off by the reader's own tick, and comes
        // back tomorrow of its own accord - finishing it here would be finishing it twice.
        var crossedOff = false;
        foreach (var item in taskList.Items.Where(item => !item.IsCompleted && RestockTaskNaming.IsRestockEntry(item.Description)))
        {
            item.Complete();
            crossedOff = true;
        }

        if (crossedOff)
        {
            await _taskRepository.UpdateAsync(taskList, cancellationToken);
        }

        return toppedUp;
    }
}
