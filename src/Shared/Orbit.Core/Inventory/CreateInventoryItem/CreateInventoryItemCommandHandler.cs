using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.CreateInventoryItem;

public sealed class CreateInventoryItemCommandHandler : IRequestHandler<CreateInventoryItemCommand, Guid>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly InventoryTaskListCoordinator _taskListCoordinator;

    public CreateInventoryItemCommandHandler(IInventoryRepository inventoryRepository, InventoryTaskListCoordinator taskListCoordinator)
    {
        _inventoryRepository = inventoryRepository;
        _taskListCoordinator = taskListCoordinator;
    }

    public async Task<Guid> HandleAsync(CreateInventoryItemCommand request, CancellationToken cancellationToken)
    {
        var item = InventoryItem.Create(
            request.UserId, request.Name, request.ProductType, request.Category, request.Quantity, request.MinimumQuantity,
            request.ExpiryDate, request.ExpiryNotificationChannel);
        await _inventoryRepository.AddAsync(item, cancellationToken);

        // Ensures the standing "keep your stock updated" reminder exists from the very first item this
        // user ever adds, independent of whether this particular item is already low - see
        // InventoryTaskListCoordinator's class comment.
        await _taskListCoordinator.EnsureManagedTaskListAsync(request.UserId, cancellationToken);

        item = await _taskListCoordinator.EnsureRestockTaskAsync(item, cancellationToken);
        if (item.PendingRestockTaskItemId is not null)
        {
            await _inventoryRepository.UpdateAsync(item, cancellationToken);
        }

        return item.Id;
    }
}
