namespace Orbit.Core.Calendar;

public interface ICalendarEventRepository
{
    /// <summary>
    /// Everything userId owns, or - when updatedSinceUtc is given - only what changed at or after it.
    /// The cursor is applied in the database: a client catching up asks for a delta, and answering it by
    /// fetching everything and discarding most of it saved the wire and nothing else.
    /// </summary>
    Task<IReadOnlyList<CalendarEvent>> GetAllAsync(
        Guid userId, DateTimeOffset? updatedSinceUtc, CancellationToken cancellationToken);

    Task<CalendarEvent?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task AddAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken);

    Task UpdateAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken);

    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken);
}
