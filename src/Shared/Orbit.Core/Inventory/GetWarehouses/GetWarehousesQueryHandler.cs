using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.GetWarehouses;

/// <summary>Thin wrapper over WarehouseAccessResolver.ResolveAllAsync - owned and shared warehouses in one flat list, mirroring GetNotesQueryHandler.</summary>
public sealed class GetWarehousesQueryHandler : IRequestHandler<GetWarehousesQuery, IReadOnlyList<Warehouse>>
{
    private readonly WarehouseAccessResolver _warehouseAccessResolver;

    public GetWarehousesQueryHandler(WarehouseAccessResolver warehouseAccessResolver)
    {
        _warehouseAccessResolver = warehouseAccessResolver;
    }

    public Task<IReadOnlyList<Warehouse>> HandleAsync(GetWarehousesQuery request, CancellationToken cancellationToken)
        => _warehouseAccessResolver.ResolveAllAsync(request.UserId, request.UpdatedSinceUtc, cancellationToken);
}
