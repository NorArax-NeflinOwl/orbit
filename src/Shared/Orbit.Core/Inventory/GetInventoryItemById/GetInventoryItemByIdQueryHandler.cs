using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.GetInventoryItemById;

public sealed class GetInventoryItemByIdQueryHandler : IRequestHandler<GetInventoryItemByIdQuery, InventoryItem?>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly WarehouseAccessResolver _warehouseAccessResolver;
    private readonly PendingRestockTaskResolver _pendingRestockTaskResolver;

    public GetInventoryItemByIdQueryHandler(
        IInventoryRepository inventoryRepository, WarehouseAccessResolver warehouseAccessResolver,
        PendingRestockTaskResolver pendingRestockTaskResolver)
    {
        _inventoryRepository = inventoryRepository;
        _warehouseAccessResolver = warehouseAccessResolver;
        _pendingRestockTaskResolver = pendingRestockTaskResolver;
    }

    public async Task<InventoryItem?> HandleAsync(GetInventoryItemByIdQuery request, CancellationToken cancellationToken)
    {
        if (await _warehouseAccessResolver.ResolveAsync(request.UserId, request.WarehouseId, cancellationToken) is null)
        {
            return null;
        }

        var item = await _inventoryRepository.GetByIdAsync(request.WarehouseId, request.Id, cancellationToken);
        if (item is null)
        {
            return null;
        }

        var hadPendingTask = item.PendingRestockTaskItemId is not null;
        item = await _pendingRestockTaskResolver.ResolveAsync(item, cancellationToken);
        if (hadPendingTask && item.PendingRestockTaskItemId is null)
        {
            await _inventoryRepository.UpdateAsync(item, cancellationToken);
        }

        return item;
    }
}
