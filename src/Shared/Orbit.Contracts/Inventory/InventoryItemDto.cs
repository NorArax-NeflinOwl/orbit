namespace Orbit.Contracts.Inventory;

public sealed record InventoryItemDto(
    Guid Id,
    string Name,
    string ProductType,
    string Category,
    decimal Quantity,
    decimal? MinimumQuantity,
    DateTimeOffset? ExpiryDate,
    string ExpiryNotificationChannel,
    bool IsBelowMinimum,
    bool HasPendingRestockTask,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
