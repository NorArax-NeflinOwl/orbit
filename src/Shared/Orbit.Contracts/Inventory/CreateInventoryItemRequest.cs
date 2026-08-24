namespace Orbit.Contracts.Inventory;

public sealed record CreateInventoryItemRequest(
    string Name, string ProductType, string Category, decimal Quantity, decimal? MinimumQuantity,
    DateTimeOffset? ExpiryDate, string ExpiryNotificationChannel);
