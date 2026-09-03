using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.AcceptInventoryShare;

public sealed class AcceptInventoryShareCommandHandler : IRequestHandler<AcceptInventoryShareCommand, bool>
{
    private readonly IInventoryShareRepository _inventoryShareRepository;

    public AcceptInventoryShareCommandHandler(IInventoryShareRepository inventoryShareRepository)
    {
        _inventoryShareRepository = inventoryShareRepository;
    }

    /// <summary>Marking the share accepted is the entire effect - it grants access to the owner's inventory, nothing is copied (see InventoryAccessResolver).</summary>
    public async Task<bool> HandleAsync(AcceptInventoryShareCommand request, CancellationToken cancellationToken)
    {
        var share = await _inventoryShareRepository.GetByIdAsync(request.RecipientUserId, request.ShareId, cancellationToken);
        if (share is null)
        {
            return false;
        }

        if (!share.IsAccepted)
        {
            share.MarkAccepted();
            await _inventoryShareRepository.UpdateAsync(share, cancellationToken);
        }

        return true;
    }
}
