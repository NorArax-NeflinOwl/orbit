using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.GetWarehouseShareStatus;

public sealed class GetWarehouseShareStatusQueryHandler : IRequestHandler<GetWarehouseShareStatusQuery, bool?>
{
    private readonly IWarehouseShareRepository _warehouseShareRepository;

    public GetWarehouseShareStatusQueryHandler(IWarehouseShareRepository warehouseShareRepository)
    {
        _warehouseShareRepository = warehouseShareRepository;
    }

    public async Task<bool?> HandleAsync(GetWarehouseShareStatusQuery request, CancellationToken cancellationToken)
    {
        var share = await _warehouseShareRepository.GetByIdAsync(request.RecipientUserId, request.ShareId, cancellationToken);
        return share?.IsAccepted;
    }
}
