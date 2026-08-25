using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.CreateInventoryItem;

public sealed class CreateInventoryItemCommandHandler : IRequestHandler<CreateInventoryItemCommand, Guid?>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly WarehouseAccessResolver _warehouseAccessResolver;
    private readonly InventoryTaskListCoordinator _taskListCoordinator;

    public CreateInventoryItemCommandHandler(
        IInventoryRepository inventoryRepository, WarehouseAccessResolver warehouseAccessResolver,
        InventoryTaskListCoordinator taskListCoordinator)
    {
        _inventoryRepository = inventoryRepository;
        _warehouseAccessResolver = warehouseAccessResolver;
        _taskListCoordinator = taskListCoordinator;
    }

    public async Task<Guid?> HandleAsync(CreateInventoryItemCommand request, CancellationToken cancellationToken)
    {
        if (await _warehouseAccessResolver.ResolveForEditAsync(request.UserId, request.WarehouseId, cancellationToken) is null)
        {
            return null;
        }

        var item = InventoryItem.Create(
            request.WarehouseId, request.Name, request.ProductType, request.Category, request.Quantity, request.MinimumQuantity,
            request.ExpiryDate, request.ExpiryNotificationChannel);
        await _inventoryRepository.AddAsync(item, cancellationToken);

        // Ensures the standing "keep your stock updated" reminder exists from the very first item added
        // to this warehouse, independent of whether this particular item is already low - see
        // InventoryTaskListCoordinator's class comment.
        await _taskListCoordinator.EnsureManagedTaskListAsync(request.WarehouseId, cancellationToken);

        item = await _taskListCoordinator.EnsureRestockTaskAsync(item, cancellationToken);
        if (item.PendingRestockTaskItemId is not null)
        {
            await _inventoryRepository.UpdateAsync(item, cancellationToken);
        }

        return item.Id;
    }
}
