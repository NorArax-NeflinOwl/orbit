using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.UpdateInventoryItem;

public sealed class UpdateInventoryItemCommandHandler : IRequestHandler<UpdateInventoryItemCommand, EditOutcome>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly InventoryTaskListCoordinator _taskListCoordinator;

    public UpdateInventoryItemCommandHandler(IInventoryRepository inventoryRepository, InventoryTaskListCoordinator taskListCoordinator)
    {
        _inventoryRepository = inventoryRepository;
        _taskListCoordinator = taskListCoordinator;
    }

    /// <summary>No locking/access-level concept here, unlike Note/TaskList/CalendarEvent - Inventory has no sharing, so NotFound is the only failure this can return.</summary>
    public async Task<EditOutcome> HandleAsync(UpdateInventoryItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _inventoryRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken);
        if (item is null)
        {
            return EditOutcome.NotFound;
        }

        item.Update(
            request.Name, request.ProductType, request.Category, request.Quantity, request.MinimumQuantity,
            request.ExpiryDate, request.ExpiryNotificationChannel);

        // Re-resolves and, if needed, creates a restock task before the single save below - whether
        // that's clearing a now-irrelevant pending reference (quantity rose back above minimum) or
        // setting a fresh one (quantity just dropped to/below it), either way it belongs in the same
        // write as the rest of this update.
        item = await _taskListCoordinator.EnsureRestockTaskAsync(item, cancellationToken);
        if (!item.IsBelowMinimum)
        {
            item.ClearPendingRestockTask();
        }

        await _inventoryRepository.UpdateAsync(item, cancellationToken);
        return EditOutcome.Success;
    }
}
