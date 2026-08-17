using Orbit.Core.Calendar;
using Orbit.Core.Calendar.Reminders;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IEventReminderRepository"/> stub for unit tests, standing in for the
/// cross-user reminder query and sent-reminder tracking EventReminderRepository backs with SQLite.
/// </summary>
internal sealed class InMemoryEventReminderRepository : IEventReminderRepository
{
    private readonly List<CalendarEvent> _calendarEvents;
    private readonly HashSet<(Guid CalendarEventId, int MinutesBeforeStart)> _sentReminders = [];

    public InMemoryEventReminderRepository(IEnumerable<CalendarEvent> calendarEvents)
    {
        _calendarEvents = calendarEvents.ToList();
    }

    public Task<IReadOnlyList<CalendarEvent>> GetAllWithRemindersConfiguredAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CalendarEvent>>(
            _calendarEvents.Where(calendarEvent => calendarEvent.Details.ReminderMinutesBeforeStart.Count > 0).ToList());

    public Task<bool> HasBeenSentAsync(Guid calendarEventId, int minutesBeforeStart, CancellationToken cancellationToken)
        => Task.FromResult(_sentReminders.Contains((calendarEventId, minutesBeforeStart)));

    public Task MarkAsSentAsync(Guid calendarEventId, int minutesBeforeStart, DateTimeOffset sentAtUtc, CancellationToken cancellationToken)
    {
        _sentReminders.Add((calendarEventId, minutesBeforeStart));
        return Task.CompletedTask;
    }
}
