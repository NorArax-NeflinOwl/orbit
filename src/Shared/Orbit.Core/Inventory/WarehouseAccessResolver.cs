using Orbit.Core.Abstractions;
using Orbit.Core.Users;

namespace Orbit.Core.Inventory;

/// <summary>
/// Loads a warehouse the way a given caller is actually allowed to see it - either because they own it,
/// or because someone shared it with them (see WarehouseShare) - and stamps the result via
/// Warehouse.SetAccessContext. Every warehouse read path and every inventory-item handler goes through
/// here instead of duplicating the owner-or-grant lookup: since items belong to a warehouse rather than
/// to a user, this resolver *is* the item authorization check too. Direct mirror of NoteAccessResolver.
/// </summary>
public sealed class WarehouseAccessResolver
{
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IWarehouseShareRepository _warehouseShareRepository;
    private readonly IUserRepository _userRepository;

    public WarehouseAccessResolver(
        IWarehouseRepository warehouseRepository, IWarehouseShareRepository warehouseShareRepository, IUserRepository userRepository)
    {
        _warehouseRepository = warehouseRepository;
        _warehouseShareRepository = warehouseShareRepository;
        _userRepository = userRepository;
    }

    /// <summary>Null when callerId neither owns warehouseId nor has an accepted share of it.</summary>
    public async Task<Warehouse?> ResolveAsync(Guid callerId, Guid warehouseId, CancellationToken cancellationToken)
    {
        var owned = await _warehouseRepository.GetByIdAsync(callerId, warehouseId, cancellationToken);
        if (owned is not null)
        {
            return owned;
        }

        var grant = await _warehouseShareRepository.FindAcceptedGrantAsync(warehouseId, callerId, cancellationToken);
        if (grant is null)
        {
            return null;
        }

        // The owner may have deleted the warehouse after granting access - a dangling grant reads as
        // "not found" rather than throwing, matching NoteAccessResolver.
        // Mirrors NoteAccessResolver: a warehouse its owner has since made private stops being reachable
        // through any grant, without the grant having to be found and deleted.
        var warehouse = await _warehouseRepository.GetByIdAsync(grant.OwnerUserId, grant.SourceWarehouseId, cancellationToken);
        if (warehouse is null || warehouse.IsPrivate)
        {
            return null;
        }

        var owner = await _userRepository.GetByIdAsync(grant.OwnerUserId, cancellationToken);
        warehouse.SetAccessContext(isShared: true, owner?.UserName, grant.AccessLevel);
        return warehouse;
    }

    /// <summary>Every warehouse callerId owns, plus every one shared with them (accepted grants only) - see Warehouses.razor.</summary>
    public async Task<IReadOnlyList<Warehouse>> ResolveAllAsync(Guid callerId, CancellationToken cancellationToken)
    {
        var owned = await _warehouseRepository.GetAllAsync(callerId, cancellationToken);
        var grants = await _warehouseShareRepository.GetAcceptedGrantsForRecipientAsync(callerId, cancellationToken);

        var granted = new List<Warehouse>();
        foreach (var grant in grants)
        {
            var warehouse = await _warehouseRepository.GetByIdAsync(grant.OwnerUserId, grant.SourceWarehouseId, cancellationToken);
            if (warehouse is null || warehouse.IsPrivate)
            {
                continue;
            }

            var owner = await _userRepository.GetByIdAsync(grant.OwnerUserId, cancellationToken);
            warehouse.SetAccessContext(isShared: true, owner?.UserName, grant.AccessLevel);
            granted.Add(warehouse);
        }

        return owned.Concat(granted).ToList();
    }

    /// <summary>
    /// Resolves a warehouse the caller is allowed to *write* to - null when they can only read it. The
    /// item handlers use this so a ReadOnly grantee can list a shared warehouse's items but not add,
    /// change, or delete any of them.
    /// </summary>
    public async Task<Warehouse?> ResolveForEditAsync(Guid callerId, Guid warehouseId, CancellationToken cancellationToken)
    {
        var warehouse = await ResolveAsync(callerId, warehouseId, cancellationToken);
        return warehouse?.AccessLevel == ShareAccessLevel.CanEdit ? warehouse : null;
    }
}
