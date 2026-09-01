using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.CreateWarehouse;

public sealed class CreateWarehouseCommandHandler : IRequestHandler<CreateWarehouseCommand, Guid>
{
    private readonly IWarehouseRepository _warehouseRepository;

    public CreateWarehouseCommandHandler(IWarehouseRepository warehouseRepository)
    {
        _warehouseRepository = warehouseRepository;
    }

    public async Task<Guid> HandleAsync(CreateWarehouseCommand request, CancellationToken cancellationToken)
    {
        var warehouse = Warehouse.Create(
            request.UserId, request.Name, request.IsPrivate, request.EncryptedContent,
            request.Description ?? string.Empty);
        await _warehouseRepository.AddAsync(warehouse, cancellationToken);
        return warehouse.Id;
    }
}
