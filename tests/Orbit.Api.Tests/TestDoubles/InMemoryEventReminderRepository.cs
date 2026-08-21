using Orbit.Core.Calendar;
using Orbit.Core.Calendar.Reminders;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IEventReminderRepository"/> stub for unit tests, standing in for the
/// cross-user reminder query and claim/send tracking EventReminderRepository backs with SQLite.
/// </summary>
internal sealed class InMemoryEventReminderRepository : IEventReminderRepository
{
    private readonly List<CalendarEvent> _calendarEvents;
    private readonly HashSet<(Guid CalendarEventId, int MinutesBeforeStart, DateTimeOffset OccurrenceStartUtc)> _claimedReminders = [];

    public InMemoryEventReminderRepository(IEnumerable<CalendarEvent> calendarEvents)
    {
        _calendarEvents = calendarEvents.ToList();
    }

    public Task<IReadOnlyList<CalendarEvent>> GetAllWithRemindersConfiguredAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CalendarEvent>>(_calendarEvents
            .Where(calendarEvent => calendarEvent.Details.ReminderMinutesBeforeStart.Count > 0 && calendarEvent.Details.NotifyBeforeStart)
            .ToList());

    public Task<bool> HasBeenSentAsync(
        Guid calendarEventId, int minutesBeforeStart, DateTimeOffset occurrenceStartUtc, CancellationToken cancellationToken)
        => Task.FromResult(_claimedReminders.Contains((calendarEventId, minutesBeforeStart, occurrenceStartUtc)));

    public Task<bool> TryClaimAsync(
        Guid calendarEventId, int minutesBeforeStart, DateTimeOffset occurrenceStartUtc, DateTimeOffset claimedAtUtc,
        CancellationToken cancellationToken)
        // HashSet<T>.Add already returns false when the item is present, which is exactly the "someone
        // else claimed this first" signal TryClaimAsync needs - no separate lookup required.
        => Task.FromResult(_claimedReminders.Add((calendarEventId, minutesBeforeStart, occurrenceStartUtc)));

    public Task ReleaseClaimAsync(
        Guid calendarEventId, int minutesBeforeStart, DateTimeOffset occurrenceStartUtc, CancellationToken cancellationToken)
    {
        _claimedReminders.Remove((calendarEventId, minutesBeforeStart, occurrenceStartUtc));
        return Task.CompletedTask;
    }
}
