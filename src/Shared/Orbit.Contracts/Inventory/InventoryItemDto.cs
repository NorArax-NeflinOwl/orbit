namespace Orbit.Contracts.Inventory;

public sealed record InventoryItemDto(
    Guid Id,
    string Name,
    string ProductType,
    string Category,
    decimal Quantity,
    decimal? MinimumQuantity,
    /// <summary>Serialized Orbit.Core.Inventory.InventoryUnit - what the two amounts above are counted in.</summary>
    string Unit,
    DateTimeOffset? ExpiryDate,
    string ExpiryNotificationChannel,
    bool IsBelowMinimum,
    bool HasPendingRestockTask,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    /// <summary>
    /// Something to look at every round rather than only when it runs low - see
    /// Orbit.Core.Inventory.InventoryItem.IsCheckedRegularly.
    /// </summary>
    bool IsCheckedRegularly = false);
