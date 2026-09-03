using Orbit.Core.Notifications;

namespace Orbit.Core.Inventories;

/// <summary>
/// One row of the inventory editor's item list on its way back to the server. Id is null for a row the
/// user just added and set for one that already exists - which is what lets UpdateInventoryCommandHandler
/// tell "create this" from "update that" without the client having to say so, and lets an existing item
/// keep its identity (and its open restock task) across a save.
/// </summary>
public sealed record InventoryItemInput(
    Guid? Id, string Name, string ProductType, string Category, decimal Quantity, decimal? MinimumQuantity,
    InventoryUnit Unit, DateTimeOffset? ExpiryDate, NotificationChannel ExpiryNotificationChannel,
    /// <summary>
    /// Null leaves the stored flag alone - see InventoryItemRequest. A save is the whole list, so a client
    /// that does not know about the flag returns each item without it, and must not thereby clear it.
    /// </summary>
    bool? IsCheckedRegularly = null);
