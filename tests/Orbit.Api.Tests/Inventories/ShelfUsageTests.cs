using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventories;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.UpdateTaskList;
using Xunit;

namespace Orbit.Api.Tests.Inventories;

/// <summary>
/// What the task lists ask of a shelf item, added up - see ShelfUsage and InventoryItem.Usage. The user's
/// rule: the shelf's minimum can never be lower than that count, and may be set higher.
/// </summary>
public sealed class ShelfUsageTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private readonly InMemoryTaskRepository _tasks = new();
    private readonly InMemoryInventoryItemRepository _shelf = new();

    private async Task<InventoryItem> AShelfItemAsync(decimal? minimum = null, decimal quantity = 0)
    {
        var item = InventoryItem.Create(
            Guid.NewGuid(), "Flour", string.Empty, null, quantity, minimum, InventoryUnit.Kilogram, null,
            NotificationChannel.None);
        await _shelf.AddAsync(item, CancellationToken.None);
        return item;
    }

    private static TaskItem AnEntryFor(InventoryItem item, decimal? needs)
        => TaskItem.Create(
            "Flour", null, false, subject: new TaskItemSubject(TaskItemKind.Inventory, linkedInventoryItemId: item.Id),
            requiredQuantity: needs);

    [Fact]
    public async Task Every_entry_standing_for_a_shelf_item_adds_its_own_minimum()
    {
        var flour = await AShelfItemAsync();
        await _tasks.AddAsync(TaskList.Create(UserId, "Bread", [AnEntryFor(flour, 2), AnEntryFor(flour, needs: null)]), CancellationToken.None);
        await _tasks.AddAsync(TaskList.Create(UserId, "Pizza", [AnEntryFor(flour, 3)]), CancellationToken.None);

        await new ShelfUsage(_tasks, _shelf).RecountAsync(UserId, new HashSet<Guid> { flour.Id }, CancellationToken.None);

        // Two, three, and one for the entry that says nothing - the counting rule's answer for a bare line.
        Assert.Equal(6, flour.Usage);
    }

    [Fact]
    public async Task The_minimum_is_never_read_as_lower_than_what_the_lists_ask_for()
    {
        var flour = await AShelfItemAsync(minimum: 2, quantity: 3);

        flour.CountUsage(5);

        Assert.Equal(5, flour.EffectiveMinimum);
        Assert.True(flour.IsBelowMinimum);
    }

    [Fact]
    public async Task A_minimum_set_higher_than_that_stays_as_set()
    {
        var flour = await AShelfItemAsync(minimum: 10);

        flour.CountUsage(5);

        Assert.Equal(10, flour.EffectiveMinimum);
    }

    /// <summary>A save of a list is what moves the count - up when an entry asks for more, and down when it goes.</summary>
    [Fact]
    public async Task Saving_a_list_recounts_what_its_entries_ask_of_the_shelf()
    {
        var flour = await AShelfItemAsync();
        var entry = AnEntryFor(flour, 2);
        var bread = TaskList.Create(UserId, "Bread", [entry]);
        await _tasks.AddAsync(bread, CancellationToken.None);

        await Handler().HandleAsync(
            new UpdateTaskListCommand(
                UserId, bread.Id, "Bread",
                [TaskItem.FromPersistence(
                    entry.Id, "Flour", null, false, linkedTaskListIds: null, reminders: null,
                    subject: new TaskItemSubject(TaskItemKind.Inventory, linkedInventoryItemId: flour.Id),
                    requiredQuantity: 4)],
                IsGroup: false, IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);
        Assert.Equal(4, flour.Usage);

        await Handler().HandleAsync(
            new UpdateTaskListCommand(UserId, bread.Id, "Bread", [], IsGroup: false, IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);
        Assert.Equal(0, flour.Usage);
    }

    private UpdateTaskListCommandHandler Handler()
        => new(
            new TaskListAccessResolver(_tasks, new InMemoryTaskListShareRepository(), new InMemoryUserRepository()),
            _tasks,
            new TaskListLinkValidator(_tasks),
            new RestockCompletion(
                new InMemoryInventoryManagedTaskListRepository(), new InMemoryInventoryItemRepository(),
                new InMemoryInventoryRepository(), new InMemoryTaskRepository()),
            new StockedEntryCompletion(new InMemoryInventoryRepository(), new InMemoryInventoryItemRepository()),
            new InventoryTestContext().ProductEntryPlacement,
            new ShelfUsage(_tasks, _shelf));
}
