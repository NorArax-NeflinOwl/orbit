using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.Reminders;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Calendar.Reminders;

public sealed class EventReminderSchedulerTests
{
    private static readonly TimeSpan LookBackWindow = TimeSpan.FromMinutes(5);
    private static readonly DateTimeOffset ArbitraryMidnightUtc = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

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
        await repository.TryClaimAsync(calendarEvent.Id, 10, calendarEvent.Details.StartUtc, now, CancellationToken.None);
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

    [Fact]
    public async Task FindDueRemindersAsync_ignores_events_with_notify_before_start_turned_off()
    {
        var now = DateTimeOffset.UtcNow;
        var calendarEvent = CreateEventStartingIn(now, TimeSpan.Zero, [0], notifyBeforeStart: false);
        var repository = new InMemoryEventReminderRepository([calendarEvent]);
        var scheduler = new EventReminderScheduler(repository);

        var dueReminders = await scheduler.FindDueRemindersAsync(now, LookBackWindow, CancellationToken.None);

        Assert.Empty(dueReminders);
    }

    [Fact]
    public async Task FindDueRemindersAsync_ignores_an_all_day_event_created_on_the_same_day_it_starts()
    {
        // An arbitrary fixed midnight, used as both the event's start and the "now" passed to
        // FindDueRemindersAsync, so the 0-minutes-before reminder is due by construction regardless of
        // the wall-clock time this test actually runs at.
        var startUtc = ArbitraryMidnightUtc;
        var createdAtUtc = startUtc.AddHours(20);
        var details = new CalendarEventDetails(
            "Holiday", null, null, null, startUtc, startUtc.AddDays(1), true, null, [], [0],
            CreationNotificationChannel: NotificationChannel.None, ReminderNotificationChannel: NotificationChannel.Email);
        // FromPersistence (rather than Create) is the only way to control CreatedAtUtc directly, which
        // this suppression rule depends on.
        var calendarEvent = CalendarEvent.FromPersistence(
            Guid.NewGuid(), Guid.NewGuid(), details, createdAtUtc, updatedAtUtc: createdAtUtc, isShared: false, sharedByUserName: null);
        var repository = new InMemoryEventReminderRepository([calendarEvent]);
        var scheduler = new EventReminderScheduler(repository);

        var dueReminders = await scheduler.FindDueRemindersAsync(startUtc, LookBackWindow, CancellationToken.None);

        Assert.Empty(dueReminders);
    }

    [Fact]
    public async Task FindDueRemindersAsync_still_returns_an_all_day_event_created_on_an_earlier_day()
    {
        var startUtc = ArbitraryMidnightUtc;
        var createdAtUtc = startUtc.AddDays(-3);
        var details = new CalendarEventDetails(
            "Holiday", null, null, null, startUtc, startUtc.AddDays(1), true, null, [], [0],
            CreationNotificationChannel: NotificationChannel.None, ReminderNotificationChannel: NotificationChannel.Email);
        var calendarEvent = CalendarEvent.FromPersistence(
            Guid.NewGuid(), Guid.NewGuid(), details, createdAtUtc, updatedAtUtc: createdAtUtc, isShared: false, sharedByUserName: null);
        var repository = new InMemoryEventReminderRepository([calendarEvent]);
        var scheduler = new EventReminderScheduler(repository);

        var dueReminders = await scheduler.FindDueRemindersAsync(startUtc, LookBackWindow, CancellationToken.None);

        var dueReminder = Assert.Single(dueReminders);
        Assert.Equal(0, dueReminder.MinutesBeforeStart);
    }

    [Fact]
    public async Task FindDueRemindersAsync_returns_a_due_reminder_for_a_later_occurrence_of_a_recurring_event()
    {
        var now = DateTimeOffset.UtcNow;
        // The event's own StartUtc is five days in the past; only today's occurrence, generated by the
        // recurrence rule, is due right now.
        var occurrenceStartUtc = now.AddMinutes(10);
        var firstOccurrenceStartUtc = occurrenceStartUtc.AddDays(-5);
        var recurrence = new EventRecurrence(RecurrenceFrequency.Daily, 1, null);
        var details = new CalendarEventDetails(
            "Standup", null, null, null, firstOccurrenceStartUtc, firstOccurrenceStartUtc.AddMinutes(30), false, recurrence, [], [10],
            CreationNotificationChannel: NotificationChannel.None, ReminderNotificationChannel: NotificationChannel.Email);
        var calendarEvent = CalendarEvent.Create(Guid.NewGuid(), details);
        var repository = new InMemoryEventReminderRepository([calendarEvent]);
        var scheduler = new EventReminderScheduler(repository);

        var dueReminders = await scheduler.FindDueRemindersAsync(now, LookBackWindow, CancellationToken.None);

        var dueReminder = Assert.Single(dueReminders);
        Assert.Equal(occurrenceStartUtc, dueReminder.OccurrenceStartUtc);
        Assert.Equal(10, dueReminder.MinutesBeforeStart);
    }

    [Fact]
    public async Task FindDueRemindersAsync_returns_independently_due_reminders_for_different_occurrences_of_the_same_recurring_event()
    {
        var (todayOccurrenceStartUtc, tomorrowOccurrenceStartUtc, repository, scheduler) = CreateTwoOccurrenceDueRemindersFixture();

        var dueReminders = await scheduler.FindDueRemindersAsync(DateTimeOffset.UtcNow, LookBackWindow, CancellationToken.None);

        Assert.Equal(2, dueReminders.Count);
        Assert.Contains(dueReminders, reminder => reminder.OccurrenceStartUtc == todayOccurrenceStartUtc && reminder.MinutesBeforeStart == 0);
        Assert.Contains(dueReminders, reminder => reminder.OccurrenceStartUtc == tomorrowOccurrenceStartUtc && reminder.MinutesBeforeStart == 1440);
    }

    [Fact]
    public async Task FindDueRemindersAsync_still_returns_another_occurrences_reminder_after_one_occurrences_reminder_was_sent()
    {
        var (todayOccurrenceStartUtc, tomorrowOccurrenceStartUtc, repository, scheduler) = CreateTwoOccurrenceDueRemindersFixture();
        var calendarEvent = Assert.Single(await repository.GetAllWithRemindersConfiguredAsync(CancellationToken.None));
        await repository.TryClaimAsync(calendarEvent.Id, 0, todayOccurrenceStartUtc, DateTimeOffset.UtcNow, CancellationToken.None);

        var dueReminders = await scheduler.FindDueRemindersAsync(DateTimeOffset.UtcNow, LookBackWindow, CancellationToken.None);

        var dueReminder = Assert.Single(dueReminders);
        Assert.Equal(tomorrowOccurrenceStartUtc, dueReminder.OccurrenceStartUtc);
    }

    [Fact]
    public async Task FindDueRemindersAsync_only_suppresses_the_all_day_creation_day_reminder_for_a_recurring_events_first_occurrence()
    {
        var firstOccurrenceStartUtc = ArbitraryMidnightUtc;
        var createdAtUtc = firstOccurrenceStartUtc.AddHours(20);
        var recurrence = new EventRecurrence(RecurrenceFrequency.Daily, 1, null);
        var details = new CalendarEventDetails(
            "Daily holiday", null, null, null, firstOccurrenceStartUtc, firstOccurrenceStartUtc.AddDays(1), true, recurrence, [], [0],
            CreationNotificationChannel: NotificationChannel.None, ReminderNotificationChannel: NotificationChannel.Email);
        var calendarEvent = CalendarEvent.FromPersistence(
            Guid.NewGuid(), Guid.NewGuid(), details, createdAtUtc, updatedAtUtc: createdAtUtc, isShared: false, sharedByUserName: null);
        var repository = new InMemoryEventReminderRepository([calendarEvent]);
        var scheduler = new EventReminderScheduler(repository);

        // The second occurrence starts a calendar day after the event was created, so it does not match
        // the "created the same day it starts" suppression rule the way the first occurrence would.
        var secondOccurrenceStartUtc = firstOccurrenceStartUtc.AddDays(1);
        var dueReminders = await scheduler.FindDueRemindersAsync(secondOccurrenceStartUtc, LookBackWindow, CancellationToken.None);

        var dueReminder = Assert.Single(dueReminders);
        Assert.Equal(secondOccurrenceStartUtc, dueReminder.OccurrenceStartUtc);
    }

    /// <summary>
    /// A daily recurring event with a 0-minute and a 1440-minute (24-hour) lead time, positioned so that
    /// today's occurrence's 0-minute reminder and tomorrow's occurrence's 1440-minute reminder are both
    /// due at once - the two lead times overlap far enough for a single poll to span both occurrences.
    /// </summary>
    private static (
        DateTimeOffset TodayOccurrenceStartUtc, DateTimeOffset TomorrowOccurrenceStartUtc, InMemoryEventReminderRepository Repository,
        EventReminderScheduler Scheduler) CreateTwoOccurrenceDueRemindersFixture()
    {
        var now = DateTimeOffset.UtcNow;
        var todayOccurrenceStartUtc = now;
        var tomorrowOccurrenceStartUtc = now.AddDays(1);
        var recurrence = new EventRecurrence(RecurrenceFrequency.Daily, 1, null);
        var details = new CalendarEventDetails(
            "All-hands", null, null, null, todayOccurrenceStartUtc, todayOccurrenceStartUtc.AddHours(1), false, recurrence, [], [0, 1440],
            CreationNotificationChannel: NotificationChannel.None, ReminderNotificationChannel: NotificationChannel.Email);
        var calendarEvent = CalendarEvent.Create(Guid.NewGuid(), details);
        var repository = new InMemoryEventReminderRepository([calendarEvent]);
        var scheduler = new EventReminderScheduler(repository);
        return (todayOccurrenceStartUtc, tomorrowOccurrenceStartUtc, repository, scheduler);
    }

    [Fact]
    public async Task FindDueRemindersAsync_caps_the_number_of_reminders_returned_at_max_results()
    {
        var now = DateTimeOffset.UtcNow;
        var calendarEvents = Enumerable.Range(0, 3)
            .Select(_ => CreateEventStartingIn(now, TimeSpan.Zero, [0]))
            .ToList();
        var repository = new InMemoryEventReminderRepository(calendarEvents);
        var scheduler = new EventReminderScheduler(repository);

        var dueReminders = await scheduler.FindDueRemindersAsync(now, LookBackWindow, CancellationToken.None, maxResults: 2);

        Assert.Equal(2, dueReminders.Count);
    }

    private static CalendarEvent CreateEventStartingIn(
        DateTimeOffset now, TimeSpan leadTime, IReadOnlyList<int> reminderMinutesBeforeStart, bool notifyBeforeStart = true)
    {
        var startUtc = now.Add(leadTime);
        var details = new CalendarEventDetails(
            "Title", null, null, null, startUtc, startUtc.AddHours(1), false, null, [], reminderMinutesBeforeStart,
            CreationNotificationChannel: NotificationChannel.None,
            ReminderNotificationChannel: notifyBeforeStart ? NotificationChannel.Email : NotificationChannel.None);
        return CalendarEvent.Create(Guid.NewGuid(), details);
    }
}
