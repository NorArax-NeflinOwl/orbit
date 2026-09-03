using Orbit.Core.Abstractions;

using Orbit.Core.Notifications;

namespace Orbit.Core.Inventories.ShareInventory;

/// <summary>
/// Shares request.InventoryId - either the caller's own, or one shared with them - under exactly the
/// rules ShareNoteCommandHandler applies to notes: the owner may share at any level to anyone but
/// themselves; a recipient may re-share only with Share or CanEdit access and never above their own
/// level; and nobody may share back to the owner. A second offer to a recipient who already has one
/// (accepted or pending) re-sends it as a reminder rather than creating a duplicate row.
/// </summary>
public sealed class ShareInventoryCommandHandler : IRequestHandler<ShareInventoryCommand, ShareOutcome?>
{
    private readonly InventoryAccessResolver _inventoryAccessResolver;
    private readonly IInventoryShareRepository _inventoryShareRepository;
    private readonly ISharedItemNotifier _sharedItemNotifier;

    public ShareInventoryCommandHandler(
        InventoryAccessResolver inventoryAccessResolver, IInventoryShareRepository inventoryShareRepository, ISharedItemNotifier sharedItemNotifier)
    {
        _inventoryAccessResolver = inventoryAccessResolver;
        _inventoryShareRepository = inventoryShareRepository;
        _sharedItemNotifier = sharedItemNotifier;
    }

    public async Task<ShareOutcome?> HandleAsync(ShareInventoryCommand request, CancellationToken cancellationToken)
    {
        var inventory = await _inventoryAccessResolver.ResolveAsync(request.OwnerUserId, request.InventoryId, cancellationToken);
        if (inventory is null)
        {
            return null;
        }

        if (inventory.IsPrivate)
        {
            // A private inventory has no readable name or items on the server and is the owner's alone
            // by definition - refused here as well as hidden in the client, so a hand-made request can't
            // create a share that would only ever hand someone ciphertext they cannot open.
            throw new InvalidRequestException("A private inventory can't be shared.");
        }

        if (request.RecipientUserId == inventory.UserId)
        {
            return null;
        }

        if (inventory.IsShared && !inventory.AccessLevel.CanGrant(request.AccessLevel))
        {
            return null;
        }

        var existingShare = await _inventoryShareRepository.FindExistingAsync(inventory.Id, request.RecipientUserId, cancellationToken);
        if (existingShare is not null)
        {
            // Sharing again at a higher level raises the existing offer rather than being a no-op:
            // that is how an owner answers a request for edit access (see RequestEditAccess), and
            // "share it with them again, but with more" is what they mean by doing it.
            var accessLevelRaised = existingShare.RaiseAccessLevelTo(request.AccessLevel);
            if (accessLevelRaised)
            {
                await _inventoryShareRepository.UpdateAsync(existingShare, cancellationToken);
            }

            return new ShareOutcome(existingShare.Id, AlreadyShared: true, accessLevelRaised);
        }

        var share = InventoryShare.Create(inventory.Id, inventory.UserId, request.RecipientUserId, request.AccessLevel);
        await _inventoryShareRepository.AddAsync(share, cancellationToken);
        await _sharedItemNotifier.NotifyAsync(
            request.RecipientUserId, request.OwnerUserId, SharedItemKind.Inventory, inventory.Name, cancellationToken);
        return new ShareOutcome(share.Id, AlreadyShared: false);
    }
}
