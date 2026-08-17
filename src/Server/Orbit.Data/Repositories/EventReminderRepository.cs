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
        // Cheap SQL-side prefilter (RemindersJson is either "[]" or a JSON array with entries) so events
        // with no reminders configured, or with "approaching event" notifications turned off, are never
        // even loaded into memory.
        var entities = await _dbContext.CalendarEvents
            .AsNoTracking()
            .Where(entity => entity.RemindersJson != "[]" && entity.NotifyBeforeStart)
            .ToListAsync(cancellationToken);

        return entities.Select(CalendarEventEntityMapper.ToDomain).ToList();
    }

    public Task<bool> HasBeenSentAsync(Guid calendarEventId, int minutesBeforeStart, CancellationToken cancellationToken)
        => _dbContext.EventReminderDeliveries
            .AsNoTracking()
            .AnyAsync(
                delivery => delivery.CalendarEventId == calendarEventId && delivery.MinutesBeforeStart == minutesBeforeStart,
                cancellationToken);

    public async Task<bool> TryClaimAsync(
        Guid calendarEventId, int minutesBeforeStart, DateTimeOffset claimedAtUtc, CancellationToken cancellationToken)
    {
        var claim = new EventReminderDeliveryEntity
        {
            Id = Guid.NewGuid(),
            CalendarEventId = calendarEventId,
            MinutesBeforeStart = minutesBeforeStart,
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
            // The unique index on (CalendarEventId, MinutesBeforeStart) rejected the insert - another
            // worker already claimed this reminder first. Detach the failed row so the change tracker
            // doesn't keep retrying it on this DbContext's next SaveChangesAsync call (this instance is
            // reused across every reminder processed in the same poll tick - see
            // CalendarEventReminderBackgroundService).
            _dbContext.Entry(claim).State = EntityState.Detached;
            return false;
        }
    }

    public async Task ReleaseClaimAsync(Guid calendarEventId, int minutesBeforeStart, CancellationToken cancellationToken)
    {
        var claim = await _dbContext.EventReminderDeliveries.FirstOrDefaultAsync(
            delivery => delivery.CalendarEventId == calendarEventId && delivery.MinutesBeforeStart == minutesBeforeStart,
            cancellationToken);

        if (claim is not null)
        {
            _dbContext.EventReminderDeliveries.Remove(claim);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
