using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.ReleaseWarehouseLock;

public sealed class ReleaseWarehouseLockCommandHandler : IRequestHandler<ReleaseWarehouseLockCommand, bool>
{
    private readonly WarehouseAccessResolver _warehouseAccessResolver;
    private readonly IWarehouseRepository _warehouseRepository;

    public ReleaseWarehouseLockCommandHandler(WarehouseAccessResolver warehouseAccessResolver, IWarehouseRepository warehouseRepository)
    {
        _warehouseAccessResolver = warehouseAccessResolver;
        _warehouseRepository = warehouseRepository;
    }

    public async Task<bool> HandleAsync(ReleaseWarehouseLockCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await _warehouseAccessResolver.ResolveAsync(request.UserId, request.WarehouseId, cancellationToken);
        if (warehouse is null)
        {
            return false;
        }

        warehouse.ReleaseLock(request.UserId);
        await _warehouseRepository.UpdateAsync(warehouse, cancellationToken);
        return true;
    }
}
