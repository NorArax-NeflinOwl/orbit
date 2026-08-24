namespace Orbit.Core.Inventory.ExpiryReminders;

/// <summary>
/// Finds inventory items nearing expiry that haven't been warned about yet for that specific expiry
/// date - the core logic behind InventoryExpiryReminderBackgroundService in Orbit.Api, kept independent
/// of ASP.NET Core hosting so it can be unit tested directly. Mirrors
/// Orbit.Core.Tasks.OverdueNotifications.OverdueTaskNotificationScheduler.
/// </summary>
public sealed class InventoryExpiryReminderScheduler
{
    /// <summary>How far ahead of ExpiryDate a warning goes out - fixed for v1, not configurable per item.</summary>
    public static readonly TimeSpan LeadTime = TimeSpan.FromDays(3);

    private readonly IInventoryExpiryNotificationRepository _inventoryExpiryNotificationRepository;

    public InventoryExpiryReminderScheduler(IInventoryExpiryNotificationRepository inventoryExpiryNotificationRepository)
    {
        _inventoryExpiryNotificationRepository = inventoryExpiryNotificationRepository;
    }

    /// <summary>
    /// <paramref name="maxResults"/> caps how many reminders a single call returns, protecting against a
    /// burst of simultaneously expiring items overwhelming the caller - anything beyond the cap is
    /// simply picked up by the next call instead of being dropped.
    /// </summary>
    public async Task<IReadOnlyList<DueExpiryReminder>> FindDueRemindersAsync(
        DateTimeOffset nowUtc, CancellationToken cancellationToken, int maxResults = int.MaxValue)
    {
        var candidates = await _inventoryExpiryNotificationRepository.GetItemsNearingExpiryAsync(nowUtc + LeadTime, cancellationToken);
        var due = new List<DueExpiryReminder>();

        foreach (var candidate in candidates)
        {
            if (due.Count >= maxResults)
            {
                break;
            }

            if (await _inventoryExpiryNotificationRepository.HasBeenNotifiedAsync(candidate.InventoryItemId, candidate.ExpiryDate, cancellationToken))
            {
                continue;
            }

            due.Add(candidate);
        }

        return due;
    }
}
