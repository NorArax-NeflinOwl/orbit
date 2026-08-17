namespace Orbit.Core.Calendar;

public interface ICalendarEventRepository
{
    Task<IReadOnlyList<CalendarEvent>> GetAllAsync(Guid userId, CancellationToken cancellationToken);

    Task<CalendarEvent?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task AddAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken);

    Task UpdateAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken);

    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken);
}
