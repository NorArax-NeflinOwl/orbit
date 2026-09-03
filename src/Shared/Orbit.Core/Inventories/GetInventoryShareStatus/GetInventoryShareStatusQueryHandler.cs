using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.GetInventoryShareStatus;

public sealed class GetInventoryShareStatusQueryHandler : IRequestHandler<GetInventoryShareStatusQuery, bool?>
{
    private readonly IInventoryShareRepository _inventoryShareRepository;

    public GetInventoryShareStatusQueryHandler(IInventoryShareRepository inventoryShareRepository)
    {
        _inventoryShareRepository = inventoryShareRepository;
    }

    public async Task<bool?> HandleAsync(GetInventoryShareStatusQuery request, CancellationToken cancellationToken)
    {
        var share = await _inventoryShareRepository.GetByIdAsync(request.RecipientUserId, request.ShareId, cancellationToken);
        return share?.IsAccepted;
    }
}
