namespace Orbit.Contracts.Inventories;

/// <summary>
/// One row of the inventory editor's item list on its way back to the server. Id is null for a row the
/// user just added; existing rows send theirs back so the item keeps its identity - and its open restock
/// task - across a save. Mirrors TaskItemDto's role in the task list editor.
/// </summary>
public sealed record InventoryItemRequest(
    Guid? Id,
    string Name,
    string ProductType,
    string Category,
    decimal Quantity,
    decimal? MinimumQuantity,
    /// <summary>Serialized Orbit.Core.Inventories.InventoryUnit - what the two amounts above are counted in.</summary>
    string Unit,
    DateTimeOffset? ExpiryDate,
    string ExpiryNotificationChannel,
    /// <summary>
    /// Something to look at every round rather than only when it runs low - see
    /// Orbit.Core.Inventories.InventoryItem.IsCheckedRegularly.
    ///
    /// <b>Null on the way in means "not provided", and leaves whatever is stored alone.</b> This DTO is
    /// both what the server sends and what a save sends back, so a client that has not learned about
    /// the flag yet returns the item without it - and must not thereby turn it off. On the way out it
    /// is always set.
    /// </summary>
    bool? IsCheckedRegularly = null);
