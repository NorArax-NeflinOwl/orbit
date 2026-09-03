using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.GetInventoryItems;

public sealed class GetInventoryItemsQueryHandler : IRequestHandler<GetInventoryItemsQuery, IReadOnlyList<InventoryItem>?>
{
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly InventoryAccessResolver _inventoryAccessResolver;
    private readonly PendingRestockTaskResolver _pendingRestockTaskResolver;

    public GetInventoryItemsQueryHandler(
        IInventoryItemRepository inventoryItemRepository, InventoryAccessResolver inventoryAccessResolver,
        PendingRestockTaskResolver pendingRestockTaskResolver)
    {
        _inventoryItemRepository = inventoryItemRepository;
        _inventoryAccessResolver = inventoryAccessResolver;
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
        if (await _inventoryAccessResolver.ResolveAsync(request.UserId, request.InventoryId, cancellationToken) is null)
        {
            return null;
        }

        var items = await _inventoryItemRepository.GetAllAsync(request.InventoryId, cancellationToken);
        var resolved = new List<InventoryItem>(items.Count);
        foreach (var item in items)
        {
            var hadPendingTask = item.PendingRestockTaskItemId is not null;
            var resolvedItem = await _pendingRestockTaskResolver.ResolveAsync(item, cancellationToken);
            if (hadPendingTask && resolvedItem.PendingRestockTaskItemId is null)
            {
                await _inventoryItemRepository.UpdateAsync(resolvedItem, cancellationToken);
            }

            resolved.Add(resolvedItem);
        }

        return resolved;
    }
}
