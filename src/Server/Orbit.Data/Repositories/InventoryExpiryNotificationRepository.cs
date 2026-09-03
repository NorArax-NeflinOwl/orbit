using Microsoft.EntityFrameworkCore;
using Orbit.Core.Inventories.ExpiryReminders;
using Orbit.Core.Notifications;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class InventoryExpiryNotificationRepository : IInventoryExpiryNotificationRepository
{
    private readonly OrbitDbContext _dbContext;

    public InventoryExpiryNotificationRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<DueExpiryReminder>> GetItemsNearingExpiryAsync(DateTimeOffset thresholdUtc, CancellationToken cancellationToken)
    {
        // Joined to Inventories because an item has no owner of its own any more - the warning goes to
        // the inventory's owner, not to everyone it happens to be shared with.
        var rows = await _dbContext.InventoryItems
            .AsNoTracking()
            .Where(item => item.ExpiryDate != null && item.ExpiryDate <= thresholdUtc && item.ExpiryNotificationChannel != "None")
            .Join(
                _dbContext.Inventories.AsNoTracking(),
                item => item.InventoryId,
                inventory => inventory.Id,
                (item, inventory) => new { Item = item, inventory.UserId })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new DueExpiryReminder(
                row.Item.Id, row.UserId, row.Item.Name, row.Item.ExpiryDate!.Value,
                Enum.Parse<NotificationChannel>(row.Item.ExpiryNotificationChannel, ignoreCase: true)))
            .ToList();
    }

    public Task<bool> HasBeenNotifiedAsync(Guid inventoryItemId, DateTimeOffset expiryDate, CancellationToken cancellationToken)
        => _dbContext.InventoryExpiryNotificationDeliveries
            .AsNoTracking()
            .AnyAsync(delivery => delivery.InventoryItemId == inventoryItemId && delivery.ExpiryDate == expiryDate, cancellationToken);

    public async Task<bool> TryClaimAsync(Guid inventoryItemId, DateTimeOffset expiryDate, DateTimeOffset claimedAtUtc, CancellationToken cancellationToken)
    {
        var claim = new InventoryExpiryNotificationDeliveryEntity
        {
            Id = Guid.NewGuid(),
            InventoryItemId = inventoryItemId,
            ExpiryDate = expiryDate,
            SentAtUtc = claimedAtUtc
        };
        _dbContext.Add(claim);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // The unique index on (InventoryItemId, ExpiryDate) rejected the insert - another worker
            // already claimed this warning first. Detach the failed row so the change tracker doesn't
            // keep retrying it on this DbContext's next SaveChangesAsync call (this instance is reused
            // across every item processed in the same poll tick).
            _dbContext.Entry(claim).State = EntityState.Detached;
            return false;
        }
    }

    public async Task ReleaseClaimAsync(Guid inventoryItemId, DateTimeOffset expiryDate, CancellationToken cancellationToken)
    {
        var claim = await _dbContext.InventoryExpiryNotificationDeliveries
            .FirstOrDefaultAsync(delivery => delivery.InventoryItemId == inventoryItemId && delivery.ExpiryDate == expiryDate, cancellationToken);

        if (claim is not null)
        {
            _dbContext.Remove(claim);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
