using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.GetInventories;

/// <summary>Thin wrapper over InventoryAccessResolver.ResolveAllAsync - owned and shared inventories in one flat list, mirroring GetNotesQueryHandler.</summary>
public sealed class GetInventoriesQueryHandler : IRequestHandler<GetInventoriesQuery, IReadOnlyList<Inventory>>
{
    private readonly InventoryAccessResolver _inventoryAccessResolver;

    public GetInventoriesQueryHandler(InventoryAccessResolver inventoryAccessResolver)
    {
        _inventoryAccessResolver = inventoryAccessResolver;
    }

    public Task<IReadOnlyList<Inventory>> HandleAsync(GetInventoriesQuery request, CancellationToken cancellationToken)
        => _inventoryAccessResolver.ResolveAllAsync(request.UserId, request.UpdatedSinceUtc, cancellationToken);
}
