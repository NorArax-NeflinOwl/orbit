namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a single tracked product, mapped separately from
/// <see cref="Orbit.Core.Inventories.InventoryItem"/> so schema changes don't force changes onto domain
/// logic, and vice versa.
/// </summary>
public sealed class InventoryItemEntity
{
    public Guid Id { get; set; }
    public Guid InventoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    /// <summary>
    /// What this is filed under - see <see cref="InventoryItemCategoryEntity"/>. Empty for one nobody
    /// has filed.
    /// </summary>
    public List<InventoryItemCategoryEntity> Categories { get; set; } = [];
    public decimal Quantity { get; set; }
    public decimal? MinimumQuantity { get; set; }

    /// <summary>Looked at every round rather than only when low - see InventoryItem.IsCheckedRegularly.</summary>
    public bool IsCheckedRegularly { get; set; }

    /// <summary>Serialized <see cref="Orbit.Core.Inventories.InventoryUnit"/> - "Piece", "Kilogram", "Litre" and so on.</summary>
    public string Unit { get; set; } = "Piece";

    public DateTimeOffset? ExpiryDate { get; set; }

    /// <summary>Serialized <see cref="Orbit.Core.Notifications.NotificationChannel"/> - "None"/"Email"/"Push"/"Both".</summary>
    public string ExpiryNotificationChannel { get; set; } = "Push";

    public Guid? PendingRestockTaskListId { get; set; }
    public Guid? PendingRestockTaskItemId { get; set; }

    /// <summary>Where the item sits on its inventory's shelf. Zero for everything stocked before shelves could be arranged.</summary>
    public int Position { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
