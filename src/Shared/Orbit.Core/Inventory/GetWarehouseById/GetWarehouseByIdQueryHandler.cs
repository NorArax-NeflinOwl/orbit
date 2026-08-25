using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.GetWarehouseById;

public sealed class GetWarehouseByIdQueryHandler : IRequestHandler<GetWarehouseByIdQuery, Warehouse?>
{
    private readonly WarehouseAccessResolver _warehouseAccessResolver;

    public GetWarehouseByIdQueryHandler(WarehouseAccessResolver warehouseAccessResolver)
    {
        _warehouseAccessResolver = warehouseAccessResolver;
    }

    public Task<Warehouse?> HandleAsync(GetWarehouseByIdQuery request, CancellationToken cancellationToken)
        => _warehouseAccessResolver.ResolveAsync(request.UserId, request.WarehouseId, cancellationToken);
}
