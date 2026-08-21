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
        var scheduler = new DailyTaskReminderScheduler(repository);

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
        var scheduler = new DailyTaskReminderScheduler(repository);

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
        var scheduler = new DailyTaskReminderScheduler(repository);

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
        var scheduler = new DailyTaskReminderScheduler(repository);

        var dueReminders = await scheduler.FindDueRemindersAsync(now, LookBackWindow, CancellationToken.None);

        Assert.Empty(dueReminders);
    }

    [Fact]
    public async Task FindDueRemindersAsync_caps_the_number_of_reminders_returned_at_max_results()
    {
        var now = DateTimeOffset.Now;
        var candidates = Enumerable.Range(0, 3).Select(_ => CreateCandidate(TimeOnly.FromDateTime(now.DateTime))).ToList();
        var repository = new InMemoryDailyTaskReminderRepository(candidates);
        var scheduler = new DailyTaskReminderScheduler(repository);

        var dueReminders = await scheduler.FindDueRemindersAsync(now, LookBackWindow, CancellationToken.None, maxResults: 2);

        Assert.Equal(2, dueReminders.Count);
    }

    private static DailyTaskReminderCandidate CreateCandidate(TimeOnly timeOfDay)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Groceries", "Buy milk", null, NotificationChannel.Push, timeOfDay);
}
