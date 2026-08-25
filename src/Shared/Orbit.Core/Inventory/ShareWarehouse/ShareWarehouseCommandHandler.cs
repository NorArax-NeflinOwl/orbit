using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.ShareWarehouse;

/// <summary>
/// Shares request.WarehouseId - either the caller's own, or one shared with them - under exactly the
/// rules ShareNoteCommandHandler applies to notes: the owner may share at any level to anyone but
/// themselves; a recipient may re-share only with Share or CanEdit access and never above their own
/// level; and nobody may share back to the owner. A second offer to a recipient who already has one
/// (accepted or pending) re-sends it as a reminder rather than creating a duplicate row.
/// </summary>
public sealed class ShareWarehouseCommandHandler : IRequestHandler<ShareWarehouseCommand, ShareOutcome?>
{
    private readonly WarehouseAccessResolver _warehouseAccessResolver;
    private readonly IWarehouseShareRepository _warehouseShareRepository;

    public ShareWarehouseCommandHandler(
        WarehouseAccessResolver warehouseAccessResolver, IWarehouseShareRepository warehouseShareRepository)
    {
        _warehouseAccessResolver = warehouseAccessResolver;
        _warehouseShareRepository = warehouseShareRepository;
    }

    public async Task<ShareOutcome?> HandleAsync(ShareWarehouseCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await _warehouseAccessResolver.ResolveAsync(request.OwnerUserId, request.WarehouseId, cancellationToken);
        if (warehouse is null)
        {
            return null;
        }

        if (request.RecipientUserId == warehouse.UserId)
        {
            return null;
        }

        if (warehouse.IsShared && (warehouse.AccessLevel < ShareAccessLevel.Share || request.AccessLevel > warehouse.AccessLevel))
        {
            return null;
        }

        var existingShare = await _warehouseShareRepository.FindExistingAsync(warehouse.Id, request.RecipientUserId, cancellationToken);
        if (existingShare is not null)
        {
            return new ShareOutcome(existingShare.Id, AlreadyShared: true);
        }

        var share = WarehouseShare.Create(warehouse.Id, warehouse.UserId, request.RecipientUserId, request.AccessLevel);
        await _warehouseShareRepository.AddAsync(share, cancellationToken);
        return new ShareOutcome(share.Id, AlreadyShared: false);
    }
}
