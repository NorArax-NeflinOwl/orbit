using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.UpdateWarehouse;

public sealed class UpdateWarehouseCommandHandler : IRequestHandler<UpdateWarehouseCommand, EditOutcome>
{
    private readonly WarehouseAccessResolver _warehouseAccessResolver;
    private readonly IWarehouseRepository _warehouseRepository;

    public UpdateWarehouseCommandHandler(WarehouseAccessResolver warehouseAccessResolver, IWarehouseRepository warehouseRepository)
    {
        _warehouseAccessResolver = warehouseAccessResolver;
        _warehouseRepository = warehouseRepository;
    }

    /// <summary>Renaming is an edit, so a ReadOnly/Share grantee gets the same NotFound a stranger does.</summary>
    public async Task<EditOutcome> HandleAsync(UpdateWarehouseCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await _warehouseAccessResolver.ResolveForEditAsync(request.UserId, request.WarehouseId, cancellationToken);
        if (warehouse is null)
        {
            return EditOutcome.NotFound;
        }

        warehouse.Update(request.Name);
        await _warehouseRepository.UpdateAsync(warehouse, cancellationToken);
        return EditOutcome.Success;
    }
}
