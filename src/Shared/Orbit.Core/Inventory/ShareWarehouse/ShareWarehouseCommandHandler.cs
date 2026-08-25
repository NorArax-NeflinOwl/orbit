using Orbit.Core.Abstractions;

using Orbit.Core.Notifications;

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
    private readonly ISharedItemNotifier _sharedItemNotifier;

    public ShareWarehouseCommandHandler(
        WarehouseAccessResolver warehouseAccessResolver, IWarehouseShareRepository warehouseShareRepository, ISharedItemNotifier sharedItemNotifier)
    {
        _warehouseAccessResolver = warehouseAccessResolver;
        _warehouseShareRepository = warehouseShareRepository;
        _sharedItemNotifier = sharedItemNotifier;
    }

    public async Task<ShareOutcome?> HandleAsync(ShareWarehouseCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await _warehouseAccessResolver.ResolveAsync(request.OwnerUserId, request.WarehouseId, cancellationToken);
        if (warehouse is null)
        {
            return null;
        }

        if (warehouse.IsPrivate)
        {
            // A private warehouse has no readable name or items on the server and is the owner's alone
            // by definition - refused here as well as hidden in the client, so a hand-made request can't
            // create a share that would only ever hand someone ciphertext they cannot open.
            throw new InvalidRequestException("A private warehouse can't be shared.");
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
        await _sharedItemNotifier.NotifyAsync(
            request.RecipientUserId, request.OwnerUserId, SharedItemKind.Warehouse, warehouse.Name, cancellationToken);
        return new ShareOutcome(share.Id, AlreadyShared: false);
    }
}
