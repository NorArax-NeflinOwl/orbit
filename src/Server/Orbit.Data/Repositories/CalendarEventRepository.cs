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

    public async Task<IReadOnlyList<CalendarEvent>> GetAllAsync(
        Guid userId, DateTimeOffset? updatedSinceUtc, CancellationToken cancellationToken)
    {
        var query = _dbContext.CalendarEvents
            .AsNoTracking()
            .Where(entity => entity.UserId == userId);

        // Narrowed in the database when the caller only wants what changed. A client catching up asks
        // for a delta; fetching everything and dropping most of it here saved the wire and nothing else.
        if (updatedSinceUtc is not null)
        {
            query = query.Where(entity => entity.UpdatedAtUtc >= updatedSinceUtc.Value);
        }

        var entities = await query.ToListAsync(cancellationToken);

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
