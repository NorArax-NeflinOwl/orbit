using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.Reminders;
using Xunit;

namespace Orbit.Api.Tests.Calendar.Reminders;

public sealed class EventReminderSchedulerTests
{
    private static readonly TimeSpan LookBackWindow = TimeSpan.FromMinutes(5);

    [Fact]
    public async Task FindDueRemindersAsync_returns_a_reminder_whose_lead_time_has_just_been_reached()
    {
        var now = DateTimeOffset.UtcNow;
        var calendarEvent = CreateEventStartingIn(now, TimeSpan.FromMinutes(10), [10]);
        var repository = new InMemoryEventReminderRepository([calendarEvent]);
        var scheduler = new EventReminderScheduler(repository);

        var dueReminders = await scheduler.FindDueRemindersAsync(now, LookBackWindow, CancellationToken.None);

        var dueReminder = Assert.Single(dueReminders);
        Assert.Equal(calendarEvent.Id, dueReminder.CalendarEvent.Id);
        Assert.Equal(10, dueReminder.MinutesBeforeStart);
    }

    [Fact]
    public async Task FindDueRemindersAsync_does_not_return_a_reminder_that_is_not_due_yet()
    {
        var now = DateTimeOffset.UtcNow;
        var calendarEvent = CreateEventStartingIn(now, TimeSpan.FromMinutes(30), [10]);
        var repository = new InMemoryEventReminderRepository([calendarEvent]);
        var scheduler = new EventReminderScheduler(repository);

        var dueReminders = await scheduler.FindDueRemindersAsync(now, LookBackWindow, CancellationToken.None);

        Assert.Empty(dueReminders);
    }

    [Fact]
    public async Task FindDueRemindersAsync_does_not_return_a_reminder_that_missed_the_look_back_window()
    {
        var now = DateTimeOffset.UtcNow;
        // The 5-minute lead time was reached 10 minutes ago - past the 5-minute look-back window.
        var calendarEvent = CreateEventStartingIn(now, TimeSpan.FromMinutes(-10), [5]);
        var repository = new InMemoryEventReminderRepository([calendarEvent]);
        var scheduler = new EventReminderScheduler(repository);

        var dueReminders = await scheduler.FindDueRemindersAsync(now, LookBackWindow, CancellationToken.None);

        Assert.Empty(dueReminders);
    }

    [Fact]
    public async Task FindDueRemindersAsync_does_not_return_a_reminder_that_was_already_sent()
    {
        var now = DateTimeOffset.UtcNow;
        var calendarEvent = CreateEventStartingIn(now, TimeSpan.FromMinutes(10), [10]);
        var repository = new InMemoryEventReminderRepository([calendarEvent]);
        await repository.MarkAsSentAsync(calendarEvent.Id, 10, now, CancellationToken.None);
        var scheduler = new EventReminderScheduler(repository);

        var dueReminders = await scheduler.FindDueRemindersAsync(now, LookBackWindow, CancellationToken.None);

        Assert.Empty(dueReminders);
    }

    [Fact]
    public async Task FindDueRemindersAsync_returns_every_currently_due_lead_time_for_the_same_event()
    {
        var now = DateTimeOffset.UtcNow;
        var calendarEvent = CreateEventStartingIn(now, TimeSpan.Zero, [0, 4]);
        var repository = new InMemoryEventReminderRepository([calendarEvent]);
        var scheduler = new EventReminderScheduler(repository);

        var dueReminders = await scheduler.FindDueRemindersAsync(now, LookBackWindow, CancellationToken.None);

        Assert.Equal(2, dueReminders.Count);
        Assert.Contains(dueReminders, reminder => reminder.MinutesBeforeStart == 0);
        Assert.Contains(dueReminders, reminder => reminder.MinutesBeforeStart == 4);
    }

    [Fact]
    public async Task FindDueRemindersAsync_ignores_events_with_no_reminders_configured()
    {
        var now = DateTimeOffset.UtcNow;
        var calendarEvent = CreateEventStartingIn(now, TimeSpan.Zero, []);
        var repository = new InMemoryEventReminderRepository([calendarEvent]);
        var scheduler = new EventReminderScheduler(repository);

        var dueReminders = await scheduler.FindDueRemindersAsync(now, LookBackWindow, CancellationToken.None);

        Assert.Empty(dueReminders);
    }

    private static CalendarEvent CreateEventStartingIn(DateTimeOffset now, TimeSpan leadTime, IReadOnlyList<int> reminderMinutesBeforeStart)
    {
        var startUtc = now.Add(leadTime);
        var details = new CalendarEventDetails(
            "Title", null, null, null, startUtc, startUtc.AddHours(1), false, null, [], reminderMinutesBeforeStart);
        return CalendarEvent.Create(Guid.NewGuid(), details);
    }
}
