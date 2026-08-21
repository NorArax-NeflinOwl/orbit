using Microsoft.EntityFrameworkCore;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks.OverdueNotifications;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class OverdueTaskNotificationRepository : IOverdueTaskNotificationRepository
{
    private readonly OrbitDbContext _dbContext;

    public OverdueTaskNotificationRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<OverdueTaskItem>> GetIncompleteWithDueDateAsync(CancellationToken cancellationToken)
    {
        // TaskItemEntity has no navigation back to its owning TaskEntity (see OrbitDbContext - only the
        // reverse Items navigation exists), so the owner's UserId and the list's Title are pulled in via
        // an explicit join on TaskId rather than a navigation property.
        var rows = await (
            from item in _dbContext.Set<TaskItemEntity>().AsNoTracking()
            join task in _dbContext.Tasks.AsNoTracking() on item.TaskId equals task.Id
            where !item.IsCompleted && item.DueDateUtc != null && item.LinkedTaskListId == null
                && item.OverdueNotificationChannel != "None"
            select new
            {
                item.Id,
                item.TaskId,
                task.UserId,
                task.Title,
                item.Description,
                item.DueDateUtc,
                item.OverdueNotificationChannel
            }).ToListAsync(cancellationToken);

        return rows
            .Select(row => new OverdueTaskItem(
                row.Id, row.TaskId, row.UserId, row.Title, row.Description, row.DueDateUtc!.Value,
                Enum.Parse<NotificationChannel>(row.OverdueNotificationChannel, ignoreCase: true)))
            .ToList();
    }

    public Task<bool> HasBeenNotifiedAsync(Guid taskItemId, CancellationToken cancellationToken)
        => _dbContext.Set<TaskOverdueNotificationDeliveryEntity>()
            .AsNoTracking()
            .AnyAsync(delivery => delivery.TaskItemId == taskItemId, cancellationToken);

    public async Task<bool> TryClaimAsync(Guid taskItemId, DateTimeOffset claimedAtUtc, CancellationToken cancellationToken)
    {
        var claim = new TaskOverdueNotificationDeliveryEntity
        {
            Id = Guid.NewGuid(),
            TaskItemId = taskItemId,
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
            // The unique index on TaskItemId rejected the insert - another worker already claimed this
            // item's overdue notification first. Detach the failed row so the change tracker doesn't
            // keep retrying it on this DbContext's next SaveChangesAsync call (this instance is reused
            // across every item processed in the same poll tick - see OverdueTaskNotificationBackgroundService).
            _dbContext.Entry(claim).State = EntityState.Detached;
            return false;
        }
    }

    public async Task ReleaseClaimAsync(Guid taskItemId, CancellationToken cancellationToken)
    {
        var claim = await _dbContext.Set<TaskOverdueNotificationDeliveryEntity>()
            .FirstOrDefaultAsync(delivery => delivery.TaskItemId == taskItemId, cancellationToken);

        if (claim is not null)
        {
            _dbContext.Remove(claim);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
