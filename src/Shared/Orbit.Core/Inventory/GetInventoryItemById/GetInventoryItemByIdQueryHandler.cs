using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.GetInventoryItemById;

public sealed class GetInventoryItemByIdQueryHandler : IRequestHandler<GetInventoryItemByIdQuery, InventoryItem?>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly PendingRestockTaskResolver _pendingRestockTaskResolver;

    public GetInventoryItemByIdQueryHandler(IInventoryRepository inventoryRepository, PendingRestockTaskResolver pendingRestockTaskResolver)
    {
        _inventoryRepository = inventoryRepository;
        _pendingRestockTaskResolver = pendingRestockTaskResolver;
    }

    public async Task<InventoryItem?> HandleAsync(GetInventoryItemByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await _inventoryRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken);
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
