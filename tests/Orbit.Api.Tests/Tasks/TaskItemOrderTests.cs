using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Tasks;
using Orbit.Data.Repositories;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// Covers the order a checklist comes back in, against a real database rather than the in-memory double
/// - the bug this pins lived entirely in storage. Saving a list deletes its rows and inserts them again,
/// so before Position existed the order was whatever the database happened to hold, and it changed every
/// time anything was saved. Ticking a box reshuffled the list under the reader's finger.
/// </summary>
public sealed class TaskItemOrderTests : IDisposable
{
    private readonly TemporarySqliteDatabase _database = new();

    [Fact]
    public async Task A_list_comes_back_in_the_order_it_was_written_in()
    {
        var repository = new TaskRepository(_database.DbContext);
        var userId = Guid.NewGuid();
        var taskList = TaskList.Create(userId, "Errands", Items("A", "B", "C", "D", "E"));
        await repository.AddAsync(taskList, CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, taskList.Id, CancellationToken.None);

        Assert.Equal(["A", "B", "C", "D", "E"], stored!.Items.Select(item => item.Description));
    }

    [Fact]
    public async Task Ticking_something_off_leaves_every_other_entry_where_it_was()
    {
        var repository = new TaskRepository(_database.DbContext);
        var userId = Guid.NewGuid();
        var taskList = TaskList.Create(userId, "Errands", Items("A", "B", "C", "D", "E"));
        await repository.AddAsync(taskList, CancellationToken.None);

        var reread = await repository.GetByIdAsync(userId, taskList.Id, CancellationToken.None);
        var withOneTicked = reread!.Items
            .Select(item => TaskItem.FromPersistence(
                item.Id, item.Description, item.DueDateUtc, item.Description == "C", item.LinkedTaskListId,
                item.OverdueNotificationChannel, item.RemindDaily, item.DailyReminderNotificationChannel,
                item.DailyReminderTimeOfDay))
            .ToList();
        reread.Update(
            reread.Title, withOneTicked, reread.IsGroup, reread.IsPrivate, reread.EncryptedContent, reread.Priority);
        await repository.UpdateAsync(reread, CancellationToken.None);

        var afterwards = await repository.GetByIdAsync(userId, taskList.Id, CancellationToken.None);

        Assert.Equal(["A", "B", "C", "D", "E"], afterwards!.Items.Select(item => item.Description));
        Assert.True(afterwards.Items.Single(item => item.Description == "C").IsCompleted);
    }

    private static IReadOnlyList<TaskItem> Items(params string[] descriptions)
        => descriptions.Select(description => TaskItem.Create(description, dueDateUtc: null, isCompleted: false)).ToList();

    public void Dispose() => _database.Dispose();
}
