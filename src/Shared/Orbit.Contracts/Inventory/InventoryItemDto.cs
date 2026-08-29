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
    DateTimeOffset UpdatedAtUtc);
