using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.GetInventoryById;

public sealed class GetInventoryByIdQueryHandler : IRequestHandler<GetInventoryByIdQuery, Inventory?>
{
    private readonly InventoryAccessResolver _inventoryAccessResolver;

    public GetInventoryByIdQueryHandler(InventoryAccessResolver inventoryAccessResolver)
    {
        _inventoryAccessResolver = inventoryAccessResolver;
    }

    public Task<Inventory?> HandleAsync(GetInventoryByIdQuery request, CancellationToken cancellationToken)
        => _inventoryAccessResolver.ResolveAsync(request.UserId, request.InventoryId, cancellationToken);
}
