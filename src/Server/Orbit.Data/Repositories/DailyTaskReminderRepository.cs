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
            // No !IsCompleted here on purpose: a finished item is due again tomorrow, and is reopened
            // by ReopenAsync when its reminder fires.
            where item.RemindDaily && !item.LinkedTaskLists.Any()
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
    public async Task ReopenAsync(Guid taskItemId, DateOnly reminderDate, CancellationToken cancellationToken)
    {
        // Loaded rather than updated in place, unlike the single-column write this used to be: the new
        // due date is computed from the entry's own reminder hour, which no ExecuteUpdate can read and
        // write in one statement portably. One row per fired reminder, so the round trip is cheap.
        var item = await _dbContext.Set<TaskItemEntity>()
            .FirstOrDefaultAsync(row => row.Id == taskItemId, cancellationToken);
        if (item is null)
        {
            return;
        }

        item.IsCompleted = false;

        // Only an entry that already carried a due date gets a new one - see the interface for why.
        if (item.DueDateUtc is not null)
        {
            var dueLocal = reminderDate.ToDateTime(
                TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(item.DailyReminderTimeOfDayMinutes)));
            // As UTC, like every other due date here: Npgsql refuses any other offset for a "timestamp
            // with time zone" column, which fails the whole save rather than just this field.
            item.DueDateUtc = new DateTimeOffset(dueLocal, DateTimeOffset.Now.Offset).ToUniversalTime();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The local calendar date as a key, pinned to UTC midnight.
    ///
    /// The offset has to be zero: DateOnly.ToDateTime gives a DateTime with Kind=Unspecified, which
    /// DateTimeOffset then stamps with the machine's local offset - and Npgsql refuses to write anything
    /// but UTC to a "timestamp with time zone", so every poll on a server not running at UTC threw
    /// before it could send a single reminder. Zero is also the only offset that keeps the stored key
    /// comparable across a daylight-saving change.
    /// </summary>
    private static DateTimeOffset ToStorageDate(DateOnly reminderDate)
        => new(reminderDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}