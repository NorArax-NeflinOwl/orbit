namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a single tracked product, mapped separately from
/// <see cref="Orbit.Core.Inventory.InventoryItem"/> so schema changes don't force changes onto domain
/// logic, and vice versa.
/// </summary>
public sealed class InventoryItemEntity
{
    public Guid Id { get; set; }
    public Guid WarehouseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal? MinimumQuantity { get; set; }
    public DateTimeOffset? ExpiryDate { get; set; }

    /// <summary>Serialized <see cref="Orbit.Core.Notifications.NotificationChannel"/> - "None"/"Email"/"Push"/"Both".</summary>
    public string ExpiryNotificationChannel { get; set; } = "Push";

    public Guid? PendingRestockTaskListId { get; set; }
    public Guid? PendingRestockTaskItemId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
