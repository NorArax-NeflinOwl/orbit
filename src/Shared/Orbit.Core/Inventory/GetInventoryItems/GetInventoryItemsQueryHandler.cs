using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.GetInventoryItems;

public sealed class GetInventoryItemsQueryHandler : IRequestHandler<GetInventoryItemsQuery, IReadOnlyList<InventoryItem>>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly PendingRestockTaskResolver _pendingRestockTaskResolver;

    public GetInventoryItemsQueryHandler(IInventoryRepository inventoryRepository, PendingRestockTaskResolver pendingRestockTaskResolver)
    {
        _inventoryRepository = inventoryRepository;
        _pendingRestockTaskResolver = pendingRestockTaskResolver;
    }

    /// <summary>
    /// Resolves every item's pending-restock-task state before returning, so a task completed or
    /// deleted from /tasks is reflected here on the very next load rather than only the next time that
    /// specific item is edited - persisting the resolved state back when it changed so it doesn't need
    /// re-resolving on every subsequent read.
    /// </summary>
    public async Task<IReadOnlyList<InventoryItem>> HandleAsync(GetInventoryItemsQuery request, CancellationToken cancellationToken)
    {
        var items = await _inventoryRepository.GetAllAsync(request.UserId, cancellationToken);
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
