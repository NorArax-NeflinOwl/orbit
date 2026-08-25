using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.GetInventoryItems;

public sealed class GetInventoryItemsQueryHandler : IRequestHandler<GetInventoryItemsQuery, IReadOnlyList<InventoryItem>?>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly WarehouseAccessResolver _warehouseAccessResolver;
    private readonly PendingRestockTaskResolver _pendingRestockTaskResolver;

    public GetInventoryItemsQueryHandler(
        IInventoryRepository inventoryRepository, WarehouseAccessResolver warehouseAccessResolver,
        PendingRestockTaskResolver pendingRestockTaskResolver)
    {
        _inventoryRepository = inventoryRepository;
        _warehouseAccessResolver = warehouseAccessResolver;
        _pendingRestockTaskResolver = pendingRestockTaskResolver;
    }

    /// <summary>
    /// Resolves every item's pending-restock-task state before returning, so a task completed or
    /// deleted from /tasks is reflected here on the very next load rather than only the next time that
    /// specific item is edited - persisting the resolved state back when it changed so it doesn't need
    /// re-resolving on every subsequent read. Read-only access is enough to list items.
    /// </summary>
    public async Task<IReadOnlyList<InventoryItem>?> HandleAsync(GetInventoryItemsQuery request, CancellationToken cancellationToken)
    {
        if (await _warehouseAccessResolver.ResolveAsync(request.UserId, request.WarehouseId, cancellationToken) is null)
        {
            return null;
        }

        var items = await _inventoryRepository.GetAllAsync(request.WarehouseId, cancellationToken);
        var resolved = new List<InventoryItem>(items.Count);
        foreach (var item in items)
        {
            var hadPendingTask = item.PendingRestockTaskItemId is not null;
            var resolvedItem = await _pendingRestockTaskResolver.ResolveAsync(item, cancellationToken);
            if (hadPendingTask && resolvedItem.PendingRestockTaskItemId is null)
            {
                await _inventoryRepository.UpdateAsync(resolvedItem, cancellationToken);
            }

            resolved.Add(resolvedItem);
        }

        return resolved;
    }
}
