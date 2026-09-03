using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.ReleaseInventoryLock;

public sealed class ReleaseInventoryLockCommandHandler : IRequestHandler<ReleaseInventoryLockCommand, bool>
{
    private readonly InventoryAccessResolver _inventoryAccessResolver;
    private readonly IInventoryRepository _inventoryRepository;

    public ReleaseInventoryLockCommandHandler(InventoryAccessResolver inventoryAccessResolver, IInventoryRepository inventoryRepository)
    {
        _inventoryAccessResolver = inventoryAccessResolver;
        _inventoryRepository = inventoryRepository;
    }

    public async Task<bool> HandleAsync(ReleaseInventoryLockCommand request, CancellationToken cancellationToken)
    {
        var inventory = await _inventoryAccessResolver.ResolveAsync(request.UserId, request.InventoryId, cancellationToken);
        if (inventory is null)
        {
            return false;
        }

        inventory.ReleaseLock(request.UserId);
        await _inventoryRepository.UpdateLockAsync(inventory, cancellationToken);
        return true;
    }
}
