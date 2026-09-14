using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks.DailyReminders;
using Xunit;

namespace Orbit.Api.Tests.Tasks.DailyReminders;

public sealed class DailyTaskReminderSchedulerTests
{
    private static readonly TimeSpan LookBackWindow = TimeSpan.FromMinutes(5);

    [Fact]
    public async Task FindDueRemindersAsync_returns_a_reminder_whose_time_of_day_has_just_been_reached()
    {
        var now = DateTimeOffset.Now;
        var candidate = CreateCandidate(TimeOnly.FromDateTime(now.DateTime));
        var repository = new InMemoryDailyTaskReminderRepository([candidate]);
        var scheduler = new DailyTaskReminderScheduler(repository, new InMemoryOverdueTaskNotificationRepository([]));

        var dueReminders = await scheduler.FindDueRemindersAsync(now, LookBackWindow, CancellationToken.None);

        var dueReminder = Assert.Single(dueReminders);
        Assert.Equal(candidate.TaskItemId, dueReminder.TaskItemId);
        Assert.Equal(DateOnly.FromDateTime(now.DateTime), dueReminder.ReminderDate);
    }

    [Fact]
    public async Task FindDueRemindersAsync_does_not_return_a_reminder_that_is_not_due_yet()
    {
        var now = DateTimeOffset.Now;
        var candidate = CreateCandidate(TimeOnly.FromDateTime(now.AddMinutes(30).DateTime));
        var repository = new InMemoryDailyTaskReminderRepository([candidate]);
        var scheduler = new DailyTaskReminderScheduler(repository, new InMemoryOverdueTaskNotificationRepository([]));

        var dueReminders = await scheduler.FindDueRemindersAsync(now, LookBackWindow, CancellationToken.None);

        Assert.Empty(dueReminders);
    }

    [Fact]
    public async Task FindDueRemindersAsync_does_not_return_a_reminder_that_missed_the_look_back_window()
    {
        var now = DateTimeOffset.Now;
        // The reminder's time of day was reached 10 minutes ago - past the 5-minute look-back window.
        var candidate = CreateCandidate(TimeOnly.FromDateTime(now.AddMinutes(-10).DateTime));
        var repository = new InMemoryDailyTaskReminderRepository([candidate]);
        var scheduler = new DailyTaskReminderScheduler(repository, new InMemoryOverdueTaskNotificationRepository([]));

        var dueReminders = await scheduler.FindDueRemindersAsync(now, LookBackWindow, CancellationToken.None);

        Assert.Empty(dueReminders);
    }

    [Fact]
    public async Task FindDueRemindersAsync_does_not_return_a_reminder_already_sent_today()
    {
        var now = DateTimeOffset.Now;
        var candidate = CreateCandidate(TimeOnly.FromDateTime(now.DateTime));
        var repository = new InMemoryDailyTaskReminderRepository([candidate]);
        await repository.TryClaimAsync(candidate.TaskItemId, DateOnly.FromDateTime(now.DateTime), now, CancellationToken.None);
        var scheduler = new DailyTaskReminderScheduler(repository, new InMemoryOverdueTaskNotificationRepository([]));

        var dueReminders = await scheduler.FindDueRemindersAsync(now, LookBackWindow, CancellationToken.None);

        Assert.Empty(dueReminders);
    }

    [Fact]
    public async Task FindDueRemindersAsync_caps_the_number_of_reminders_returned_at_max_results()
    {
        var now = DateTimeOffset.Now;
        var candidates = Enumerable.Range(0, 3).Select(_ => CreateCandidate(TimeOnly.FromDateTime(now.DateTime))).ToList();
        var repository = new InMemoryDailyTaskReminderRepository(candidates);
        var scheduler = new DailyTaskReminderScheduler(repository, new InMemoryOverdueTaskNotificationRepository([]));

        var dueReminders = await scheduler.FindDueRemindersAsync(now, LookBackWindow, CancellationToken.None, maxResults: 2);

        Assert.Equal(2, dueReminders.Count);
    }

    /// <summary>
    /// An entry that is both late and reminded daily used to say the same thing twice, a minute apart -
    /// the daily reminder and the overdue notice, each right on its own. The overdue notice speaks; this
    /// one stands down for the day it goes out.
    /// </summary>
    [Fact]
    public async Task FindDueRemindersAsync_stands_down_while_an_overdue_notice_is_about_to_go_out()
    {
        var now = DateTimeOffset.Now;
        var candidate = CreateCandidate(TimeOnly.FromDateTime(now.DateTime), dueAt: now.AddHours(-1));
        var scheduler = new DailyTaskReminderScheduler(
            new InMemoryDailyTaskReminderRepository([candidate]),
            // Nothing claimed: the notice has not been sent, which is exactly the state the overdue
            // scheduler answers "send it" to - so the two would land together.
            new InMemoryOverdueTaskNotificationRepository([]));

        var dueReminders = await scheduler.FindDueRemindersAsync(now, LookBackWindow, CancellationToken.None);

        Assert.Empty(dueReminders);
    }

    /// <summary>
    /// And only for that day. The overdue notice is sent once ever, so the day after it has gone out
    /// there is nothing for the daily reminder to collide with - standing down for good would take away
    /// the "remind daily" the reader turned on.
    /// </summary>
    [Fact]
    public async Task FindDueRemindersAsync_carries_on_once_the_overdue_notice_has_gone_out()
    {
        var now = DateTimeOffset.Now;
        var candidate = CreateCandidate(TimeOnly.FromDateTime(now.DateTime), dueAt: now.AddDays(-1));
        var overdue = new InMemoryOverdueTaskNotificationRepository([]);
        // Claiming it is what the overdue service does as it sends - see its background service.
        await overdue.TryClaimAsync(candidate.TaskItemId, now, CancellationToken.None);
        var scheduler = new DailyTaskReminderScheduler(
            new InMemoryDailyTaskReminderRepository([candidate]), overdue);

        var dueReminders = await scheduler.FindDueRemindersAsync(now, LookBackWindow, CancellationToken.None);

        Assert.Equal(candidate.TaskItemId, Assert.Single(dueReminders).TaskItemId);
    }

    /// <summary>
    /// An entry with no deadline can never be overdue, so nothing ever speaks for it but this - which is
    /// the standing "Update stock levels" a shelf keeps, and most of what "remind daily" is used for.
    /// </summary>
    [Fact]
    public async Task FindDueRemindersAsync_still_reminds_about_an_entry_with_no_deadline()
    {
        var now = DateTimeOffset.Now;
        var candidate = CreateCandidate(TimeOnly.FromDateTime(now.DateTime));
        var scheduler = new DailyTaskReminderScheduler(
            new InMemoryDailyTaskReminderRepository([candidate]),
            new InMemoryOverdueTaskNotificationRepository([]));

        var dueReminders = await scheduler.FindDueRemindersAsync(now, LookBackWindow, CancellationToken.None);

        Assert.Single(dueReminders);
    }

    /// <summary>A deadline still ahead is not overdue either, so the day's reminder goes out as it always did.</summary>
    [Fact]
    public async Task FindDueRemindersAsync_still_reminds_about_an_entry_whose_deadline_is_ahead()
    {
        var now = DateTimeOffset.Now;
        var candidate = CreateCandidate(TimeOnly.FromDateTime(now.DateTime), dueAt: now.AddDays(1));
        var scheduler = new DailyTaskReminderScheduler(
            new InMemoryDailyTaskReminderRepository([candidate]),
            new InMemoryOverdueTaskNotificationRepository([]));

        var dueReminders = await scheduler.FindDueRemindersAsync(now, LookBackWindow, CancellationToken.None);

        Assert.Single(dueReminders);
    }

    private static DailyTaskReminderCandidate CreateCandidate(TimeOnly timeOfDay, DateTimeOffset? dueAt = null)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Groceries", "Buy milk", dueAt, NotificationChannel.Push, timeOfDay);
}
