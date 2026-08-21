using Microsoft.EntityFrameworkCore;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks.DailyReminders;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class DailyTaskReminderRepository : IDailyTaskReminderRepository
{
    private readonly OrbitDbContext _dbContext;

    public DailyTaskReminderRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<DailyTaskReminderCandidate>> GetEligibleAsync(CancellationToken cancellationToken)
    {
        // TaskItemEntity has no navigation back to its owning TaskEntity (see OrbitDbContext - only the
        // reverse Items navigation exists), so the owner's UserId and the list's Title are pulled in via
        // an explicit join on TaskId rather than a navigation property (mirrors OverdueTaskNotificationRepository).
        var rows = await (
            from item in _dbContext.Set<TaskItemEntity>().AsNoTracking()
            join task in _dbContext.Tasks.AsNoTracking() on item.TaskId equals task.Id
            where item.RemindDaily && !item.IsCompleted && item.LinkedTaskListId == null
                && item.DailyReminderNotificationChannel != "None"
            select new
            {
                item.Id,
                item.TaskId,
                task.UserId,
                task.Title,
                item.Description,
                item.DueDateUtc,
                item.DailyReminderNotificationChannel,
                item.DailyReminderTimeOfDayMinutes
            }).ToListAsync(cancellationToken);

        return rows
            .Select(row => new DailyTaskReminderCandidate(
                row.Id,
                row.TaskId,
                row.UserId,
                row.Title,
                row.Description,
                row.DueDateUtc,
                Enum.Parse<NotificationChannel>(row.DailyReminderNotificationChannel, ignoreCase: true),
                TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(row.DailyReminderTimeOfDayMinutes))))
            .ToList();
    }

    public Task<bool> HasBeenSentAsync(Guid taskItemId, DateOnly reminderDate, CancellationToken cancellationToken)
    {
        var storedReminderDate = ToStorageDate(reminderDate);
        return _dbContext.Set<TaskDailyReminderDeliveryEntity>()
            .AsNoTracking()
            .AnyAsync(delivery => delivery.TaskItemId == taskItemId && delivery.ReminderDate == storedReminderDate, cancellationToken);
    }

    public async Task<bool> TryClaimAsync(
        Guid taskItemId, DateOnly reminderDate, DateTimeOffset claimedAtUtc, CancellationToken cancellationToken)
    {
        var claim = new TaskDailyReminderDeliveryEntity
        {
            Id = Guid.NewGuid(),
            TaskItemId = taskItemId,
            ReminderDate = ToStorageDate(reminderDate),
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
            // The unique index on (TaskItemId, ReminderDate) rejected the insert - another worker already
            // claimed this day's reminder first. Detach the failed row so the change tracker doesn't keep
            // retrying it on this DbContext's next SaveChangesAsync call (this instance is reused across
            // every reminder processed in the same poll tick - see DailyTaskReminderBackgroundService).
            _dbContext.Entry(claim).State = EntityState.Detached;
            return false;
        }
    }

    public async Task ReleaseClaimAsync(Guid taskItemId, DateOnly reminderDate, CancellationToken cancellationToken)
    {
        var storedReminderDate = ToStorageDate(reminderDate);
        var claim = await _dbContext.Set<TaskDailyReminderDeliveryEntity>()
            .FirstOrDefaultAsync(
                delivery => delivery.TaskItemId == taskItemId && delivery.ReminderDate == storedReminderDate, cancellationToken);

        if (claim is not null)
        {
            _dbContext.Remove(claim);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// A local calendar date has no inherent DateTimeOffset representation, so it's stored as that date's
    /// local midnight - matching how other local-date concepts in this codebase (e.g. an all-day calendar
    /// event's StartUtc, per CalendarEventEditor.razor's ToDateTimeOffset) are anchored to local midnight
    /// rather than UTC midnight.
    /// </summary>
    private static DateTimeOffset ToStorageDate(DateOnly reminderDate) => new(reminderDate.ToDateTime(TimeOnly.MinValue));
}
