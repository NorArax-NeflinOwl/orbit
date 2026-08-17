using Orbit.Core.Calendar;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="ICalendarEventRepository"/> stub for unit tests that need real add/lookup/update
/// behavior, including per-user ownership scoping, without spinning up SQLite.
/// </summary>
internal sealed class InMemoryCalendarEventRepository : ICalendarEventRepository
{
    private readonly List<CalendarEvent> _calendarEvents = [];

    public Task<IReadOnlyList<CalendarEvent>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CalendarEvent>>(_calendarEvents.Where(calendarEvent => calendarEvent.UserId == userId).ToList());

    public Task<CalendarEvent?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_calendarEvents.FirstOrDefault(calendarEvent => calendarEvent.Id == id && calendarEvent.UserId == userId));

    public Task AddAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        _calendarEvents.Add(calendarEvent);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        // Handlers mutate the same CalendarEvent instance this repository already holds a reference to,
        // so there is nothing to replace here - this mirrors how the EF Core repository just calls
        // SaveChangesAsync on an already-tracked entity.
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        _calendarEvents.RemoveAll(calendarEvent => calendarEvent.Id == id && calendarEvent.UserId == userId);
        return Task.CompletedTask;
    }
}
