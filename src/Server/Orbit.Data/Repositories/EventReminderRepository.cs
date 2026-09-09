using Microsoft.EntityFrameworkCore;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.Reminders;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class EventReminderRepository : IEventReminderRepository
{
    private readonly OrbitDbContext _dbContext;

    public EventReminderRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetAllWithRemindersConfiguredAsync(CancellationToken cancellationToken)
    {
        // An appointment a task list raised is done when its entry is ticked off, whatever the clock
        // says - the shopping was done on Tuesday for a slot booked on Friday - and so is one on a list
        // its owner has closed. Reminding somebody about either is reminding them of work they have
        // already reported doing. An event of its own has no entry to have been ticked off and is never
        // in here.
        var alreadyDone =
            from item in _dbContext.Set<TaskItemEntity>().AsNoTracking()
            join task in _dbContext.Tasks.AsNoTracking() on item.TaskId equals task.Id
            // Given up on counts as finished with, the same way a tick does - see TaskItem.IsFailed.
            where item.LinkedCalendarEventId != null && (item.IsCompleted || item.IsFailed || task.IsCompleted)
            select item.LinkedCalendarEventId!.Value;

        // Cheap SQL-side prefilter (RemindersJson is either "[]" or a JSON array with entries) so events
        // with no reminders configured, or with "approaching event" notifications turned off for every
        // channel, are never even loaded into memory. NotifyAtStart counts as one configured: it is a
        // reminder zero minutes before the start, and it leaves RemindersJson empty.
        var entities = await _dbContext.CalendarEvents
            .AsNoTracking()
            .Where(entity => (entity.RemindersJson != "[]" || entity.NotifyAtStart)
                && entity.ReminderNotificationChannel != "None"
                && !alreadyDone.Contains(entity.Id))
            .ToListAsync(cancellationToken);

        return entities.Select(CalendarEventEntityMapper.ToDomain).ToList();
    }

    public Task<bool> HasBeenSentAsync(
        Guid calendarEventId, int minutesBeforeStart, DateTimeOffset occurrenceStartUtc, CancellationToken cancellationToken)
        => _dbContext.EventReminderDeliveries
            .AsNoTracking()
            .AnyAsync(
                delivery => delivery.CalendarEventId == calendarEventId && delivery.MinutesBeforeStart == minutesBeforeStart
                    && delivery.OccurrenceStartUtc == occurrenceStartUtc,
                cancellationToken);

    public async Task<bool> TryClaimAsync(
        Guid calendarEventId, int minutesBeforeStart, DateTimeOffset occurrenceStartUtc, DateTimeOffset claimedAtUtc,
        CancellationToken cancellationToken)
    {
        var claim = new EventReminderDeliveryEntity
        {
            Id = Guid.NewGuid(),
            CalendarEventId = calendarEventId,
            MinutesBeforeStart = minutesBeforeStart,
            OccurrenceStartUtc = occurrenceStartUtc,
            SentAtUtc = claimedAtUtc
        };
        _dbContext.EventReminderDeliveries.Add(claim);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // The unique index on (CalendarEventId, MinutesBeforeStart, OccurrenceStartUtc) rejected the
            // insert - another worker already claimed this reminder first. Detach the failed row so the
            // change tracker doesn't keep retrying it on this DbContext's next SaveChangesAsync call (this
            // instance is reused across every reminder processed in the same poll tick - see
            // CalendarEventReminderBackgroundService).
            _dbContext.Entry(claim).State = EntityState.Detached;
            return false;
        }
    }

    public async Task ReleaseClaimAsync(
        Guid calendarEventId, int minutesBeforeStart, DateTimeOffset occurrenceStartUtc, CancellationToken cancellationToken)
    {
        var claim = await _dbContext.EventReminderDeliveries.FirstOrDefaultAsync(
            delivery => delivery.CalendarEventId == calendarEventId && delivery.MinutesBeforeStart == minutesBeforeStart
                && delivery.OccurrenceStartUtc == occurrenceStartUtc,
            cancellationToken);

        if (claim is not null)
        {
            _dbContext.EventReminderDeliveries.Remove(claim);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
