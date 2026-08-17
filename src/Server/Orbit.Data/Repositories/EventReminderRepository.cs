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
        // Cheap SQL-side prefilter (RemindersJson is either "[]" or a JSON array with entries) so
        // events with no reminders configured - the common case - are never even loaded into memory.
        var entities = await _dbContext.CalendarEvents
            .AsNoTracking()
            .Where(entity => entity.RemindersJson != "[]")
            .ToListAsync(cancellationToken);

        return entities.Select(CalendarEventEntityMapper.ToDomain).ToList();
    }

    public Task<bool> HasBeenSentAsync(Guid calendarEventId, int minutesBeforeStart, CancellationToken cancellationToken)
        => _dbContext.EventReminderDeliveries
            .AsNoTracking()
            .AnyAsync(
                delivery => delivery.CalendarEventId == calendarEventId && delivery.MinutesBeforeStart == minutesBeforeStart,
                cancellationToken);

    public async Task MarkAsSentAsync(Guid calendarEventId, int minutesBeforeStart, DateTimeOffset sentAtUtc, CancellationToken cancellationToken)
    {
        _dbContext.EventReminderDeliveries.Add(new EventReminderDeliveryEntity
        {
            Id = Guid.NewGuid(),
            CalendarEventId = calendarEventId,
            MinutesBeforeStart = minutesBeforeStart,
            SentAtUtc = sentAtUtc
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
