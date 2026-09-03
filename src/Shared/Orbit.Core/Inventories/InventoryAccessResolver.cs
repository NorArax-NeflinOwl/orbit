using Orbit.Core.Abstractions;
using Orbit.Core.Users;

namespace Orbit.Core.Inventories;

/// <summary>
/// Loads an inventory the way a given caller is actually allowed to see it - either because they own it,
/// or because someone shared it with them (see InventoryShare) - and stamps the result via
/// Inventory.SetAccessContext. Every inventory read path and every inventory-item handler goes through
/// here instead of duplicating the owner-or-grant lookup: since items belong to an inventory rather than
/// to a user, this resolver *is* the item authorization check too. Direct mirror of NoteAccessResolver.
/// </summary>
public sealed class InventoryAccessResolver
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryShareRepository _inventoryShareRepository;
    private readonly IUserRepository _userRepository;

    public InventoryAccessResolver(
        IInventoryRepository inventoryRepository, IInventoryShareRepository inventoryShareRepository, IUserRepository userRepository)
    {
        _inventoryRepository = inventoryRepository;
        _inventoryShareRepository = inventoryShareRepository;
        _userRepository = userRepository;
    }

    /// <summary>Null when callerId neither owns inventoryId nor has an accepted share of it.</summary>
    public async Task<Inventory?> ResolveAsync(Guid callerId, Guid inventoryId, CancellationToken cancellationToken)
    {
        var owned = await _inventoryRepository.GetByIdAsync(callerId, inventoryId, cancellationToken);
        if (owned is not null)
        {
            var sharedOut = await _inventoryShareRepository.GetSharedOutInventoryIdsAsync(callerId, cancellationToken);
            owned.SetSharedWithOthers(sharedOut.Contains(inventoryId));
            return owned;
        }

        var grant = await _inventoryShareRepository.FindAcceptedGrantAsync(inventoryId, callerId, cancellationToken);
        if (grant is null)
        {
            return null;
        }

        // The owner may have deleted the inventory after granting access - a dangling grant reads as
        // "not found" rather than throwing, matching NoteAccessResolver.
        // Mirrors NoteAccessResolver: an inventory its owner has since made private stops being reachable
        // through any grant, without the grant having to be found and deleted.
        var inventory = await _inventoryRepository.GetByIdAsync(grant.OwnerUserId, grant.SourceInventoryId, cancellationToken);
        if (inventory is null || inventory.IsPrivate)
        {
            return null;
        }

        var owner = await _userRepository.GetByIdAsync(grant.OwnerUserId, cancellationToken);
        inventory.SetAccessContext(isShared: true, owner?.UserName, grant.AccessLevel);
        return inventory;
    }

    /// <summary>Every inventory callerId owns, plus every one shared with them (accepted grants only) - see Inventories.razor.</summary>
    public async Task<IReadOnlyList<Inventory>> ResolveAllAsync(
        Guid callerId, DateTimeOffset? updatedSinceUtc, CancellationToken cancellationToken)
    {
        var owned = await _inventoryRepository.GetAllAsync(callerId, updatedSinceUtc, cancellationToken);
        var grants = await _inventoryShareRepository.GetAcceptedGrantsForRecipientAsync(callerId, cancellationToken);

        // Asked once for the whole list rather than per item - see GetSharedOutInventoryIdsAsync.
        var sharedOutIds = await _inventoryShareRepository.GetSharedOutInventoryIdsAsync(callerId, cancellationToken);
        foreach (var item in owned)
        {
            item.SetSharedWithOthers(sharedOutIds.Contains(item.Id));
        }

        var granted = new List<Inventory>();
        foreach (var grant in grants)
        {
            var inventory = await _inventoryRepository.GetByIdAsync(grant.OwnerUserId, grant.SourceInventoryId, cancellationToken);
            if (inventory is null || inventory.IsPrivate)
            {
                continue;
            }

            var owner = await _userRepository.GetByIdAsync(grant.OwnerUserId, cancellationToken);
            inventory.SetAccessContext(isShared: true, owner?.UserName, grant.AccessLevel);
            granted.Add(inventory);
        }

        return owned.Concat(granted).ToList();
    }

    /// <summary>
    /// Resolves an inventory the caller is allowed to *write* to - null when they can only read it. The
    /// item handlers use this so a ReadOnly grantee can list a shared inventory's items but not add,
    /// change, or delete any of them.
    /// </summary>
    public async Task<Inventory?> ResolveForEditAsync(Guid callerId, Guid inventoryId, CancellationToken cancellationToken)
    {
        var inventory = await ResolveAsync(callerId, inventoryId, cancellationToken);
        return inventory is not null && inventory.AccessLevel.AllowsEditing() ? inventory : null;
    }
}
