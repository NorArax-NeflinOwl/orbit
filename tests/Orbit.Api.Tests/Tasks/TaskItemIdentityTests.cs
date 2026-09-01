using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventory;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.UpdateTaskList;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// What happens now that clients name their own entries.
///
/// They do so because an entry written with no connection has to have an identity from the moment it
/// exists - otherwise nothing on that device can point at it and still be pointing at it after the
/// first successful push, which is what made offline work hard. The cost is that two clients can hand
/// over the same id, and these are the rules for when they do.
/// </summary>
public sealed class TaskItemIdentityTests
{
    [Fact]
    public void An_id_a_client_chose_is_kept()
    {
        var chosen = Guid.NewGuid();
        var resolved = TaskItemIdentity.Resolve([ItemNamed(chosen, "Buy milk")], []);

        Assert.Equal(chosen, Assert.Single(resolved.Items).Id);
        Assert.Empty(resolved.ListsToSaveToo);
    }

    /// <summary>
    /// Neither keeps it, deliberately. The server cannot tell which entry the reader meant, and letting
    /// either keep the id would silently make one stand for the other's history.
    /// </summary>
    [Fact]
    public void Two_entries_claiming_one_id_both_get_a_new_one()
    {
        var contested = Guid.NewGuid();
        var elsewhere = ListHolding(ItemNamed(contested, "Buy bread"));

        var resolved = TaskItemIdentity.Resolve([ItemNamed(contested, "Buy milk")], [elsewhere]);

        Assert.NotEqual(contested, Assert.Single(resolved.Items).Id);
        Assert.NotEqual(contested, Assert.Single(elsewhere.Items).Id);
        Assert.Same(elsewhere, Assert.Single(resolved.ListsToSaveToo));
    }

    /// <summary>What each entry *is* survives being renamed - only the name changes.</summary>
    [Fact]
    public void Renaming_keeps_everything_else_about_the_entries()
    {
        var contested = Guid.NewGuid();
        var elsewhere = ListHolding(ItemNamed(contested, "Buy bread"));

        var resolved = TaskItemIdentity.Resolve([ItemNamed(contested, "Buy milk")], [elsewhere]);

        Assert.Equal("Buy milk", Assert.Single(resolved.Items).Description);
        Assert.Equal("Buy bread", Assert.Single(elsewhere.Items).Description);
    }

    /// <summary>The other way two entries can collide: one payload naming the same entry twice.</summary>
    [Fact]
    public void One_payload_claiming_an_id_twice_renames_both_of_them()
    {
        var contested = Guid.NewGuid();

        var resolved = TaskItemIdentity.Resolve(
            [ItemNamed(contested, "Buy milk"), ItemNamed(contested, "Buy bread")], []);

        Assert.All(resolved.Items, item => Assert.NotEqual(contested, item.Id));
        Assert.Equal(2, resolved.Items.Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public void An_entry_with_no_id_yet_is_left_for_the_server_to_name()
    {
        var resolved = TaskItemIdentity.Resolve(
            [ItemNamed(Guid.Empty, "Buy milk"), ItemNamed(Guid.Empty, "Buy bread")], []);

        // Two empties are not a collision - neither is claiming anything.
        Assert.Empty(resolved.ListsToSaveToo);
        Assert.All(resolved.Items, item => Assert.Equal(Guid.Empty, item.Id));
    }

    /// <summary>End to end through the handler, so the second list is actually saved rather than only renamed.</summary>
    [Fact]
    public async Task Saving_a_list_writes_the_other_one_that_had_to_be_renamed()
    {
        var contested = Guid.NewGuid();
        var context = new TaskListContext();
        var elsewhere = await context.AddListAsync("Weekend", ItemNamed(contested, "Buy bread"));
        var beingSaved = await context.AddListAsync("Saturday");

        await context.SaveAsync(beingSaved.Id, [ItemNamed(contested, "Buy milk")]);

        var stored = await context.FindAsync(elsewhere.Id);
        Assert.NotEqual(contested, Assert.Single(stored!.Items).Id);
        Assert.Equal("Buy bread", Assert.Single(stored.Items).Description);
    }

    private static TaskItem ItemNamed(Guid id, string description)
        => TaskItem.FromPersistence(
            id, description, dueDateUtc: null, isCompleted: false, linkedTaskListIds: null,
            Orbit.Core.Notifications.NotificationChannel.Push, remindDaily: false,
            Orbit.Core.Notifications.NotificationChannel.Push, dailyReminderTimeOfDay: default);

    private static TaskList ListHolding(params TaskItem[] items)
        => TaskList.Create(Guid.NewGuid(), "Weekend", items);

    private sealed class TaskListContext
    {
        private readonly InMemoryTaskRepository _taskRepository = new();
        private readonly Guid _userId = Guid.NewGuid();

        public async Task<TaskList> AddListAsync(string title, params TaskItem[] items)
        {
            var taskList = TaskList.Create(_userId, title, items);
            await _taskRepository.AddAsync(taskList, CancellationToken.None);
            return taskList;
        }

        public Task SaveAsync(Guid listId, IReadOnlyList<TaskItem> items)
            => new UpdateTaskListCommandHandler(
                    new TaskListAccessResolver(
                        _taskRepository, new InMemoryTaskListShareRepository(), new InMemoryUserRepository()),
                    _taskRepository,
                    new TaskListLinkValidator(_taskRepository),
                    new RestockCompletion(
                        new InMemoryInventoryManagedTaskListRepository(), new InMemoryInventoryRepository(),
                        new InMemoryWarehouseRepository(), new InMemoryTaskRepository()))
                .HandleAsync(
                    new UpdateTaskListCommand(
                        _userId, listId, "Saturday", items, IsGroup: false, IsPrivate: false, null),
                    CancellationToken.None);

        public Task<TaskList?> FindAsync(Guid listId)
            => _taskRepository.GetByIdAsync(_userId, listId, CancellationToken.None);
    }
}
