namespace Orbit.Core.Inventories.ExpiryReminders;

/// <summary>
/// Finds inventory items nearing expiry that haven't been warned about yet for that specific expiry
/// date - the core logic behind InventoryExpiryReminderBackgroundService in Orbit.Api, kept independent
/// of ASP.NET Core hosting so it can be unit tested directly. Mirrors
/// Orbit.Core.Tasks.OverdueNotifications.OverdueTaskNotificationScheduler.
///
/// A row the shelf holds none of is left alone, whatever its date says - see the rule in
/// <see cref="FindDueRemindersAsync"/>.
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

            // Nothing goes off when there is none of it. A row at zero is a product the shelf remembers
            // rather than one it holds - the name, the unit and the date it kept until are what let the
            // next delivery be counted in - and warning about it is warning about food nobody has.
            // Reported on 2026-09-18 with a shelf whose zeroed rows were still being warned about.
            //
            // Not a filter in the query: the rule is about what a warning means, so it is said here where
            // it can be read and tested, and the repository goes on answering "what is near its date".
            // Restocking makes the row eligible again on the next sweep with no reset - the claim is
            // keyed by the date, so a date that has not moved is still only ever warned about once.
            if (candidate.Quantity <= 0)
            {
                continue;
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
