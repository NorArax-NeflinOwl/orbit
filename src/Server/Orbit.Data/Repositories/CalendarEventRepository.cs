using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orbit.Core.Calendar;
using Orbit.Data.Entities;

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
            .Select(ToDomain)
            .ToList();
    }

    public async Task<CalendarEvent?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.CalendarEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(calendarEvent => calendarEvent.Id == id && calendarEvent.UserId == userId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        _dbContext.CalendarEvents.Add(ToEntity(calendarEvent));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        _dbContext.CalendarEvents.Update(ToEntity(calendarEvent));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static CalendarEvent ToDomain(CalendarEventEntity entity)
    {
        var recurrence = entity.RecurrenceFrequency is null
            ? null
            : new EventRecurrence(
                Enum.Parse<RecurrenceFrequency>(entity.RecurrenceFrequency),
                entity.RecurrenceIntervalCount ?? 1,
                entity.RecurrenceUntilUtc);

        var details = new CalendarEventDetails(
            entity.Title,
            entity.Description,
            entity.Location,
            entity.Color,
            entity.StartUtc,
            entity.EndUtc,
            entity.IsAllDay,
            recurrence,
            JsonSerializer.Deserialize<List<string>>(entity.GuestsJson) ?? [],
            JsonSerializer.Deserialize<List<int>>(entity.RemindersJson) ?? []);

        return CalendarEvent.FromPersistence(entity.Id, entity.UserId, details, entity.CreatedAtUtc, entity.UpdatedAtUtc);
    }

    private static CalendarEventEntity ToEntity(CalendarEvent calendarEvent)
    {
        var details = calendarEvent.Details;
        return new CalendarEventEntity
        {
            Id = calendarEvent.Id,
            UserId = calendarEvent.UserId,
            Title = details.Title,
            Description = details.Description,
            Location = details.Location,
            Color = details.Color,
            StartUtc = details.StartUtc,
            EndUtc = details.EndUtc,
            IsAllDay = details.IsAllDay,
            RecurrenceFrequency = details.Recurrence?.Frequency.ToString(),
            RecurrenceIntervalCount = details.Recurrence?.IntervalCount,
            RecurrenceUntilUtc = details.Recurrence?.UntilUtc,
            GuestsJson = JsonSerializer.Serialize(details.Guests),
            RemindersJson = JsonSerializer.Serialize(details.ReminderMinutesBeforeStart),
            CreatedAtUtc = calendarEvent.CreatedAtUtc,
            UpdatedAtUtc = calendarEvent.UpdatedAtUtc
        };
    }
}
