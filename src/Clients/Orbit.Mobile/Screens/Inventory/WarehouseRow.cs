using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Inventory;

/// <summary>One row of the warehouses screen - the same shape as the other three features' rows.</summary>
public sealed record WarehouseRow(
    Guid LocalId, string Name, int ItemCount, bool HasUnsentChanges, OfflineEditRefusal Refusal)
{
    public static WarehouseRow From(LocalWarehouse warehouse, bool hasUnsentChanges, INetworkStatus networkStatus)
        => new(
            warehouse.LocalId, warehouse.Name, warehouse.Items.Count, hasUnsentChanges,
            OfflineEditPolicy.Evaluate(warehouse, networkStatus));

    public string Contents => ItemCount == 1 ? "1 item" : $"{ItemCount} items";

    /// <summary>Empty when there is nothing worth saying, which is the common case.</summary>
    public string Status => Refusal switch
    {
        OfflineEditRefusal.SharedWithYou => "Shared with you - read-only until you're back online",
        OfflineEditRefusal.SharedWithOthers => "Shared with others - read-only until you're back online",
        _ => HasUnsentChanges ? "Waiting to sync" : string.Empty
    };

    public bool HasStatus => Status.Length > 0;
}
