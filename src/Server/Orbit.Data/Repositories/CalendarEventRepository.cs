using Microsoft.EntityFrameworkCore;
using Orbit.Core.Calendar;

namespace Orbit.Data.Repositories;

public sealed class CalendarEventRepository : ICalendarEventRepository
{
    private readonly OrbitDbContext _dbContext;

    public CalendarEventRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        // SQLite can't translate ORDER BY on a DateTimeOffset column, so the sort has to happen in
        // memory after fetching (see the EF Core NotSupportedException this avoids) - same reason
        // NoteRepository/TaskRepository sort after ToListAsync.
        var entities = await _dbContext.CalendarEvents
            .AsNoTracking()
            .Where(entity => entity.UserId == userId)
            .ToListAsync(cancellationToken);

        return entities
            .OrderBy(entity => entity.StartUtc)
            .Select(CalendarEventEntityMapper.ToDomain)
            .ToList();
    }

    public async Task<CalendarEvent?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.CalendarEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(calendarEvent => calendarEvent.Id == id && calendarEvent.UserId == userId, cancellationToken);

        return entity is null ? null : CalendarEventEntityMapper.ToDomain(entity);
    }

    public async Task AddAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        _dbContext.CalendarEvents.Add(CalendarEventEntityMapper.ToEntity(calendarEvent));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        _dbContext.CalendarEvents.Update(CalendarEventEntityMapper.ToEntity(calendarEvent));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.CalendarEvents
            .FirstOrDefaultAsync(calendarEvent => calendarEvent.Id == id && calendarEvent.UserId == userId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        _dbContext.CalendarEvents.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
