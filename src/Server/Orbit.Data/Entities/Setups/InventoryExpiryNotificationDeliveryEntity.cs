namespace Orbit.Data.Entities;

/// <summary>
/// Reserves one inventory item's expiry warning for a given expiry date, and doubles as the permanent
/// record that it was sent - mirrors <see cref="TaskDailyReminderDeliveryEntity"/>. Keyed by
/// (InventoryItemId, ExpiryDate) rather than InventoryItemId alone, so restocking an item with a new
/// expiry date is automatically eligible for a fresh warning with no explicit reset needed. The row is
/// inserted by InventoryExpiryNotificationRepository.TryClaimAsync before the warning actually goes
/// out, so the unique index on that pair is what stops two concurrent
/// InventoryExpiryReminderBackgroundService instances from ever sending the same warning twice.
/// </summary>
public sealed class InventoryExpiryNotificationDeliveryEntity
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public DateTimeOffset ExpiryDate { get; set; }
    public DateTimeOffset SentAtUtc { get; set; }
}
