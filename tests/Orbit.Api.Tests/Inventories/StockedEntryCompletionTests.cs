using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.UpdateTaskList;
using Xunit;

namespace Orbit.Api.Tests.Inventories;

/// <summary>
/// An inventory entry crossing itself off once the shelf it points at holds what it asked to keep. What
/// somebody typed on the entry - "there are four, keep two" - is an answer, and a list that goes on
/// asking for it is a list nobody reads to the bottom of.
/// </summary>
public sealed class StockedEntryCompletionTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly InventoryTestContext _context = new();

    /// <summary>A row on a shelf of this reader's, with the two numbers that decide everything here.</summary>
    private async Task<InventoryItem> AShelfItemAsync(
        decimal quantity, decimal? minimumQuantity, bool isCheckedRegularly = false)
    {
        var inventoryId = _context.AddInventory(_userId);
        var item = InventoryItem.Create(
            inventoryId, "Zupka Buldog", "Food", ["Dry goods"], quantity, minimumQuantity,
            InventoryUnit.Piece, expiryDate: null, NotificationChannel.None, position: 0, isCheckedRegularly);
        await _context.InventoryItemRepository.AddAsync(item, CancellationToken.None);
        return item;
    }

    /// <summary>An entry standing for a row on a shelf - what generating a storage from a list leaves behind.</summary>
    private static TaskItem StandingFor(InventoryItem shelfItem, bool isCompleted = false)
        => TaskItem.Create(
            shelfItem.Name, dueDateUtc: null, isCompleted,
            subject: new TaskItemSubject(TaskItemKind.Inventory, linkedInventoryItemId: shelfItem.Id));

    private Task<bool> CrossOffAsync(params TaskItem[] items)
        => _context.StockedEntryCompletion.CrossOffWhatTheShelfCoversAsync(_userId, items, CancellationToken.None);

    [Fact]
    public async Task An_entry_whose_shelf_holds_what_it_asked_for_is_crossed_off()
    {
        var entry = StandingFor(await AShelfItemAsync(quantity: 4, minimumQuantity: 2));

        Assert.True(await CrossOffAsync(entry));

        Assert.True(entry.IsCompleted);
    }

    /// <summary>Exactly the minimum is enough: the minimum is how little is too little, not how little is wanted.</summary>
    [Fact]
    public async Task Exactly_the_minimum_counts_as_covered()
    {
        var entry = StandingFor(await AShelfItemAsync(quantity: 2, minimumQuantity: 2));

        Assert.True(await CrossOffAsync(entry));

        Assert.True(entry.IsCompleted);
    }

    [Fact]
    public async Task An_entry_whose_shelf_is_short_stays_outstanding()
    {
        var entry = StandingFor(await AShelfItemAsync(quantity: 1, minimumQuantity: 2));

        Assert.False(await CrossOffAsync(entry));

        Assert.False(entry.IsCompleted);
    }

    /// <summary>
    /// A row with no minimum was left to be counted instead - "leave the minimum empty to have it
    /// counted" - so there is no amount here that settles anything, and the entry is left alone.
    /// </summary>
    [Fact]
    public async Task A_row_that_was_never_given_a_minimum_answers_nothing()
    {
        var entry = StandingFor(await AShelfItemAsync(quantity: 40, minimumQuantity: null));

        Assert.False(await CrossOffAsync(entry));

        Assert.False(entry.IsCompleted);
    }

    /// <summary>Crossing one of these off answers "have you looked", which a count cannot answer for anybody.</summary>
    [Fact]
    public async Task A_row_to_be_looked_at_every_round_answers_nothing()
    {
        var entry = StandingFor(await AShelfItemAsync(quantity: 9, minimumQuantity: 1, isCheckedRegularly: true));

        Assert.False(await CrossOffAsync(entry));

        Assert.False(entry.IsCompleted);
    }

    /// <summary>
    /// The other direction is nobody's to take: a tick is somebody's own answer, and a restock errand
    /// that is crossed off is what tells the shelf it was filled - see RestockCompletion.
    /// </summary>
    [Fact]
    public async Task A_crossed_off_entry_is_never_brought_back()
    {
        var entry = StandingFor(await AShelfItemAsync(quantity: 0, minimumQuantity: 5), isCompleted: true);

        Assert.False(await CrossOffAsync(entry));

        Assert.True(entry.IsCompleted);
    }

    /// <summary>An entry that names something and points at nothing has no shelf to answer for it.</summary>
    [Fact]
    public async Task An_entry_pointing_at_no_shelf_item_is_left_alone()
    {
        await AShelfItemAsync(quantity: 4, minimumQuantity: 2);
        var entry = TaskItem.Create(
            "Zupka Buldog", dueDateUtc: null, isCompleted: false,
            subject: new TaskItemSubject(TaskItemKind.Inventory));

        Assert.False(await CrossOffAsync(entry));

        Assert.False(entry.IsCompleted);
    }

    [Fact]
    public async Task An_ordinary_checklist_line_is_left_alone()
    {
        await AShelfItemAsync(quantity: 4, minimumQuantity: 2);
        var entry = TaskItem.Create("Zupka Buldog", dueDateUtc: null, isCompleted: false);

        Assert.False(await CrossOffAsync(entry));

        Assert.False(entry.IsCompleted);
    }

    /// <summary>
    /// The whole way through, on the path that runs every time: somebody saves the list, and the entry
    /// the shelf covers comes back crossed off. Written with the same save rather than a second one -
    /// see UpdateTaskListCommandHandler.
    /// </summary>
    [Fact]
    public async Task Saving_a_list_crosses_off_what_the_shelf_covers()
    {
        var shelfItem = await AShelfItemAsync(quantity: 4, minimumQuantity: 2);
        var taskList = TaskList.Create(_userId, "Zakupy", [StandingFor(shelfItem)]);
        await _context.TaskRepository.AddAsync(taskList, CancellationToken.None);

        var outcome = await new UpdateTaskListCommandHandler(
                new TaskListAccessResolver(
                    _context.TaskRepository, new InMemoryTaskListShareRepository(), new InMemoryUserRepository()),
                _context.TaskRepository,
                new TaskListLinkValidator(_context.TaskRepository),
                _context.RestockCompletion,
                _context.StockedEntryCompletion)
            .HandleAsync(
                new UpdateTaskListCommand(
                    _userId, taskList.Id, taskList.Title, [.. taskList.Items], IsGroup: false, IsPrivate: false,
                    EncryptedContent: null),
                CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
        var stored = await _context.TaskRepository.GetByIdAsync(_userId, taskList.Id, CancellationToken.None);
        Assert.True(Assert.Single(stored!.Items).IsCompleted);
        Assert.True(stored.IsCompleted);
    }

    /// <summary>Somebody else's shelf answers nothing here - the storages read are the list owner's own.</summary>
    [Fact]
    public async Task Another_readers_shelf_answers_nothing()
    {
        var inventoryId = _context.AddInventory(Guid.NewGuid());
        var theirs = InventoryItem.Create(
            inventoryId, "Zupka Buldog", "Food", null, quantity: 4, minimumQuantity: 2,
            InventoryUnit.Piece, expiryDate: null, NotificationChannel.None);
        await _context.InventoryItemRepository.AddAsync(theirs, CancellationToken.None);
        var entry = StandingFor(theirs);

        Assert.False(await CrossOffAsync(entry));

        Assert.False(entry.IsCompleted);
    }
}
