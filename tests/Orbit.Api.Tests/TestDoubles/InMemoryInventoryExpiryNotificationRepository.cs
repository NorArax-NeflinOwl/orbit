using Orbit.Core.Inventory.ExpiryReminders;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IInventoryExpiryNotificationRepository"/> stub for unit tests, standing in for
/// the cross-user near-expiry query and claim/send tracking InventoryExpiryNotificationRepository backs
/// with Postgres (mirrors <see cref="InMemoryOverdueTaskNotificationRepository"/>, but keyed on the
/// (item, expiry date) pair instead of the item id alone).
/// </summary>
internal sealed class InMemoryInventoryExpiryNotificationRepository : IInventoryExpiryNotificationRepository
{
    private readonly List<DueExpiryReminder> _candidates;
    private readonly HashSet<(Guid InventoryItemId, DateTimeOffset ExpiryDate)> _claimedKeys = [];

    public InMemoryInventoryExpiryNotificationRepository(IEnumerable<DueExpiryReminder> candidates)
    {
        _candidates = candidates.ToList();
    }

    public Task<IReadOnlyList<DueExpiryReminder>> GetItemsNearingExpiryAsync(DateTimeOffset thresholdUtc, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<DueExpiryReminder>>(_candidates.Where(candidate => candidate.ExpiryDate <= thresholdUtc).ToList());

    public Task<bool> HasBeenNotifiedAsync(Guid inventoryItemId, DateTimeOffset expiryDate, CancellationToken cancellationToken)
        => Task.FromResult(_claimedKeys.Contains((inventoryItemId, expiryDate)));

    public Task<bool> TryClaimAsync(Guid inventoryItemId, DateTimeOffset expiryDate, DateTimeOffset claimedAtUtc, CancellationToken cancellationToken)
        // HashSet<T>.Add already returns false when the key is present, which is exactly the "someone
        // else claimed this first" signal TryClaimAsync needs - no separate lookup required.
        => Task.FromResult(_claimedKeys.Add((inventoryItemId, expiryDate)));

    public Task ReleaseClaimAsync(Guid inventoryItemId, DateTimeOffset expiryDate, CancellationToken cancellationToken)
    {
        _claimedKeys.Remove((inventoryItemId, expiryDate));
        return Task.CompletedTask;
    }
}
