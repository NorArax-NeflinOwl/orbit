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
        // Which lists an inventory keeps for itself - the one place an entry that comes round again
        // lives, and the only thing here that is work rather than an errand. See ComesRoundAgain below.
        var managedTaskListIds = _dbContext.InventoryManagedTaskLists.AsNoTracking().Select(managed => managed.TaskListId);

        var rows = await (
            from item in _dbContext.Set<TaskItemEntity>().AsNoTracking()
            join task in _dbContext.Tasks.AsNoTracking() on item.TaskId equals task.Id
            // A list somebody closed is not owed any more, so it stops asking: saying "no more of this"
            // and then being reminded of it every morning is the app arguing with the reader.
            // An entry done by ways that include a list is left out with the linked ones: its stored tick
            // cannot know that list is finished - see TaskItemAlternativeEntity.IsDone.
            where item.RemindDaily && !task.IsCompleted && !item.LinkedTaskLists.Any()
                && !item.Alternatives.Any(way => way.LinkedTaskListId != null)
                && item.DailyReminderNotificationChannel != "None"
                // Finished with, either way, and the asking stops - unless the entry is work that happens
                // again tomorrow whatever was done about it today, which on the shelf's standing round is
                // the whole point. See ComesRoundAgain, and the decision recorded in info/future-plan.md.
                && (!item.IsCompleted && !item.IsFailed
                    || (item.Description == StandingRoundDescription && managedTaskListIds.Contains(item.TaskId)))
            select new
            {
                item.Id,
                item.TaskId,
                task.UserId,
                task.Title,
                item.Description,
                item.DueDateUtc,
                item.DailyReminderNotificationChannel,
                item.DailyReminderTimeOfDayMinutes,
                ComesRoundAgain = item.Description == StandingRoundDescription && managedTaskListIds.Contains(item.TaskId)
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
                TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(row.DailyReminderTimeOfDayMinutes)),
                row.ComesRoundAgain))
            .ToList();
    }

    /// <summary>
    /// The standing round on a shelf's restock list, by the words the server writes it with - see
    /// Orbit.Core.Inventories.RestockTaskNaming.UpdateStockReminderDescription, which is a constant
    /// rather than anything a reader typed (a reader's language is put over it on the way out, see
    /// OrbitWrittenNames). Matched together with the list being one an inventory keeps, so an entry
    /// somebody happens to name the same thing on a list of their own is still their errand.
    /// </summary>
    private const string StandingRoundDescription = Orbit.Core.Inventories.RestockTaskNaming.UpdateStockReminderDescription;

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
            .Include(row => row.Alternatives)
            .FirstOrDefaultAsync(row => row.Id == taskItemId, cancellationToken);
        if (item is null)
        {
            return;
        }

        // Everything TaskItem.Reopen clears, and for the reasons it gives there. Written out again here
        // rather than called, because this path holds the row and not the aggregate - which is why it
        // has to be kept level with that method by hand, and why TaskItemReopeningTests pins the two
        // together.
        //
        // Neither ticked nor crossed out: a daily errand comes round again whatever yesterday's answer
        // was. And no time for being done, which belongs to something that is done - an entry left
        // carrying one read as not done while its own page still said when it had been finished.
        item.IsCompleted = false;
        item.IsFailed = false;
        item.CompletedAtUtc = null;

        // An entry done one of several ways comes back with none of them taken: the ways stay, since
        // they are what the entry is, and the choice is made again. Left ticked, they were an entry
        // that said it was not done above a full set of ways saying it was - and the next save reads
        // the tick back off them.
        foreach (var way in item.Alternatives)
        {
            way.IsDone = false;
        }

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