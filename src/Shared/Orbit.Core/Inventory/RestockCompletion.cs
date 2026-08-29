namespace Orbit.Core.Inventory;

/// <summary>
/// What finishing a restock errand means to the shelf it came from. Crossing off "Restock: Flour (5)"
/// says somebody went and got the flour, so the shelf is brought up to the level it is meant to hold -
/// otherwise the reader has to say the same thing twice, once on the list and once in the warehouse,
/// and the errand comes straight back the next time stock is checked.
///
/// Only ever raises an amount. Finishing an errand is not a claim about how much is there beyond the
/// minimum, and somebody who stocked more than that should not have it taken away.
/// </summary>
public sealed class RestockCompletion
{
    private readonly IInventoryManagedTaskListRepository _managedTaskListRepository;
    private readonly IInventoryRepository _inventoryRepository;

    public RestockCompletion(
        IInventoryManagedTaskListRepository managedTaskListRepository, IInventoryRepository inventoryRepository)
    {
        _managedTaskListRepository = managedTaskListRepository;
        _inventoryRepository = inventoryRepository;
    }

    /// <summary>
    /// Tops up whatever the entries just crossed off on <paramref name="taskListId"/> stand for, and
    /// answers how many items that was. Nothing happens for an ordinary task list, which is the case
    /// almost every save takes.
    /// </summary>
    public async Task<int> ApplyAsync(
        Guid taskListId, IReadOnlyCollection<Guid> completedTaskItemIds, CancellationToken cancellationToken)
    {
        if (completedTaskItemIds.Count == 0)
        {
            return 0;
        }

        if (await _managedTaskListRepository.GetWarehouseIdAsync(taskListId, cancellationToken) is not { } warehouseId)
        {
            return 0;
        }

        var toppedUp = 0;
        foreach (var item in await _inventoryRepository.GetAllAsync(warehouseId, cancellationToken))
        {
            if (item.PendingRestockTaskItemId is not { } taskItemId || !completedTaskItemIds.Contains(taskItemId))
            {
                continue;
            }

            if (!item.TopUpToMinimum())
            {
                continue;
            }

            await _inventoryRepository.UpdateAsync(item, cancellationToken);
            toppedUp += 1;
        }

        return toppedUp;
    }

    /// <summary>
    /// Brings every item in the warehouse behind <paramref name="taskListId"/> up to its minimum - the
    /// answer to "yes, I have restocked everything" rather than crossing off one errand at a time.
    /// </summary>
    public async Task<int> TopUpEverythingAsync(Guid taskListId, CancellationToken cancellationToken)
    {
        if (await _managedTaskListRepository.GetWarehouseIdAsync(taskListId, cancellationToken) is not { } warehouseId)
        {
            return 0;
        }

        var toppedUp = 0;
        foreach (var item in await _inventoryRepository.GetAllAsync(warehouseId, cancellationToken))
        {
            if (!item.TopUpToMinimum())
            {
                continue;
            }

            await _inventoryRepository.UpdateAsync(item, cancellationToken);
            toppedUp += 1;
        }

        return toppedUp;
    }
}
