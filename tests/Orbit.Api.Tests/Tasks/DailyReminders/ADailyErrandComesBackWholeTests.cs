using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Data.Repositories;
using Xunit;

namespace Orbit.Api.Tests.Tasks.DailyReminders;

/// <summary>
/// What bringing a daily errand back leaves behind, against a real database: the reminder loop reopens
/// the row itself (DailyTaskReminderRepository.ReopenAsync) rather than going through the aggregate, so
/// nothing but a test keeps the two from drifting - and they had. The row's reopen cleared the tick and
/// the cross and stopped there, where TaskItem.Reopen also clears the time it was done and every way it
/// was done by.
/// </summary>
public sealed class ADailyErrandComesBackWholeTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 9, 16);
    private static readonly DateTimeOffset Yesterday = new(2026, 9, 15, 9, 0, 0, TimeSpan.Zero);

    private readonly TemporarySqliteDatabase _database = new();

    /// <summary>
    /// A time for being done belongs to something that is done - see TaskItem.CompletedAtUtc, whose own
    /// summary page reads it without asking whether the entry is ticked. Left behind, a reopened entry
    /// sat there not done, above a line saying when it had been finished.
    /// </summary>
    [Fact]
    public async Task An_entry_brought_back_no_longer_says_when_it_was_done()
    {
        var taskList = await StoreAsync(TaskItem.Create(
            "Update stock levels", Yesterday, isCompleted: true,
            reminders: Reminders(), completedAtUtc: Yesterday));

        await ReopenAsync(taskList);

        var entry = await RereadAsync(taskList);
        Assert.False(entry.IsCompleted);
        Assert.Null(entry.CompletedAtUtc);
    }

    /// <summary>
    /// An entry done one of several ways comes back with none of them taken. Left ticked, the ways said
    /// the entry was done while the entry said it was not - and the entry's tick is the ways', so the
    /// next save reads it straight back off them.
    /// </summary>
    [Fact]
    public async Task An_entry_brought_back_has_none_of_its_ways_taken()
    {
        var taskList = await StoreAsync(TaskItem.Create(
            "Get there", Yesterday, isCompleted: true, reminders: Reminders(),
            alternatives: [new TaskItemAlternative("By bus", IsDone: true), new TaskItemAlternative("On foot")],
            completedAtUtc: Yesterday));

        await ReopenAsync(taskList);

        var entry = await RereadAsync(taskList);
        Assert.False(entry.IsCompleted);
        Assert.All(entry.Alternatives, way => Assert.False(way.IsDone));
        Assert.Equal(["By bus", "On foot"], entry.Alternatives.Select(way => way.Description));
    }

    /// <summary>A cross is a way of being finished with something, so it goes too - the rule for a tick.</summary>
    [Fact]
    public async Task An_entry_given_up_on_comes_back_as_work()
    {
        var taskList = await StoreAsync(TaskItem.Create(
            "Impregnate the boots", Yesterday, isCompleted: false, reminders: Reminders(), isFailed: true));

        await ReopenAsync(taskList);

        var entry = await RereadAsync(taskList);
        Assert.False(entry.IsFailed);
        Assert.False(entry.IsCompleted);
    }

    private static TaskItemReminders Reminders()
        => new(NotificationChannel.Push, Daily: true, NotificationChannel.Push, new TimeOnly(9, 0));

    private async Task<TaskList> StoreAsync(TaskItem entry)
    {
        var taskList = TaskList.Create(Guid.NewGuid(), "Errands", [entry]);
        await new TaskRepository(_database.DbContext).AddAsync(taskList, CancellationToken.None);
        return taskList;
    }

    private Task ReopenAsync(TaskList taskList)
        => new DailyTaskReminderRepository(
                _database.DbContext,
                new LinkedEntryCompletion(new TaskRepository(_database.DbContext), new LinkedTaskCompletionResolver()))
            .ReopenAsync(taskList.Items.Single().Id, Today, CancellationToken.None);

    private async Task<TaskItem> RereadAsync(TaskList taskList)
    {
        var stored = await new TaskRepository(_database.DbContext)
            .GetByIdAsync(taskList.UserId, taskList.Id, CancellationToken.None);
        return stored!.Items.Single();
    }

    public void Dispose() => _database.Dispose();
}
