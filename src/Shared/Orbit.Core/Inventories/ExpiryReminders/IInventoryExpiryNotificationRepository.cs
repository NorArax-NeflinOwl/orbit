namespace Orbit.Core.Inventories.ExpiryReminders;

/// <summary>
/// Backs InventoryExpiryReminderScheduler: finds every inventory item (across all users) whose expiry
/// date falls within the warning lead time, and coordinates which (item, expiry date) pairs have
/// already been warned about - keyed by the expiry date value itself (not the item id alone), so
/// restocking an item with a new expiry date is automatically eligible for a fresh warning with no
/// explicit reset needed. Mirrors IOverdueTaskNotificationRepository.
/// </summary>
public interface IInventoryExpiryNotificationRepository
{
    /// <summary>Every item with ExpiryDate set, at or before thresholdUtc, and a non-None ExpiryNotificationChannel.</summary>
    Task<IReadOnlyList<DueExpiryReminder>> GetItemsNearingExpiryAsync(DateTimeOffset thresholdUtc, CancellationToken cancellationToken);

    Task<bool> HasBeenNotifiedAsync(Guid inventoryItemId, DateTimeOffset expiryDate, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically reserves a single (item, expiry date) pair's warning for the caller to send, using a
    /// unique constraint on that pair as the concurrency guard. Returns false without throwing when
    /// another worker already reserved (or sent) it first.
    /// </summary>
    Task<bool> TryClaimAsync(Guid inventoryItemId, DateTimeOffset expiryDate, DateTimeOffset claimedAtUtc, CancellationToken cancellationToken);

    /// <summary>Releases a reservation made by TryClaimAsync that failed to actually send, so it's retried on a later poll instead of being silently lost.</summary>
    Task ReleaseClaimAsync(Guid inventoryItemId, DateTimeOffset expiryDate, CancellationToken cancellationToken);
}
