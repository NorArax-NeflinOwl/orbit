using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.AcceptWarehouseShare;

public sealed class AcceptWarehouseShareCommandHandler : IRequestHandler<AcceptWarehouseShareCommand, bool>
{
    private readonly IWarehouseShareRepository _warehouseShareRepository;

    public AcceptWarehouseShareCommandHandler(IWarehouseShareRepository warehouseShareRepository)
    {
        _warehouseShareRepository = warehouseShareRepository;
    }

    /// <summary>Marking the share accepted is the entire effect - it grants access to the owner's warehouse, nothing is copied (see WarehouseAccessResolver).</summary>
    public async Task<bool> HandleAsync(AcceptWarehouseShareCommand request, CancellationToken cancellationToken)
    {
        var share = await _warehouseShareRepository.GetByIdAsync(request.RecipientUserId, request.ShareId, cancellationToken);
        if (share is null)
        {
            return false;
        }

        if (!share.IsAccepted)
        {
            share.MarkAccepted();
            await _warehouseShareRepository.UpdateAsync(share, cancellationToken);
        }

        return true;
    }
}
