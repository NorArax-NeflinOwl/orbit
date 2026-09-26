using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;
using Orbit.Core.Inventories.UpdateInventory;
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
        decimal quantity, decimal? minimumQuantity, bool isCheckedRegularly = false,
        DateTimeOffset? expiryDate = null)
    {
        var inventoryId = _context.AddInventory(_userId);
        var item = InventoryItem.Create(
            inventoryId, "Zupka Buldog", "Food", ["Dry goods"], quantity, minimumQuantity,
            InventoryUnit.Piece, expiryDate, NotificationChannel.None, position: 0, isCheckedRegularly);
        await _context.InventoryItemRepository.AddAsync(item, CancellationToken.None);
        return item;
    }

    /// <summary>
    /// The start of a day some way behind us, which is how a use-by date is stored - see
    /// InventoryItem.HasExpired, and the browser's ToExpiryOffset, which makes one out of a date box.
    /// </summary>
    private static DateTimeOffset UseByDaysAgo(int days)
        => new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).AddDays(-days);

    /// <summary>An entry standing for a row on a shelf - what generating a storage from a list leaves behind.</summary>
    private static TaskItem StandingFor(InventoryItem shelfItem, bool isCompleted = false)
        => TaskItem.Create(
            shelfItem.Name, dueDateUtc: null, isCompleted,
            subject: new TaskItemSubject(TaskItemKind.Inventory, linkedInventoryItemId: shelfItem.Id));

    private Task<bool> CrossOffAsync(params TaskItem[] items)
        => _context.StockedEntryCompletion.SettleWhatTheShelfSaysAsync(_userId, items, CancellationToken.None);

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

    /// <summary>
    /// Holding four of something is not the same as holding four of it that are any good. A row past its
    /// use-by date crosses the entry <em>out</em> - finished with, and not done - where until 2026-09-24
    /// the count alone crossed it off. See StockedEntryCompletion, and InventoryItem.HasExpired.
    /// </summary>
    [Fact]
    public async Task A_shelf_row_past_its_date_crosses_the_entry_out_rather_than_off()
    {
        var entry = StandingFor(await AShelfItemAsync(quantity: 4, minimumQuantity: 2, expiryDate: UseByDaysAgo(3)));

        Assert.True(await CrossOffAsync(entry));

        Assert.True(entry.IsFailed);
        Assert.False(entry.IsCompleted);
        Assert.True(entry.IsResolved);
    }

    /// <summary>
    /// Good all through the day it names. A date is stored as the start of that day, so comparing the
    /// stored moment against now would call a thing marked "use by today" expired at one minute past
    /// midnight - see InventoryItem.HasExpired.
    /// </summary>
    [Fact]
    public async Task A_row_whose_date_is_today_is_still_good()
    {
        var entry = StandingFor(await AShelfItemAsync(quantity: 4, minimumQuantity: 2, expiryDate: UseByDaysAgo(0)));

        Assert.True(await CrossOffAsync(entry));

        Assert.True(entry.IsCompleted);
        Assert.False(entry.IsFailed);
    }

    /// <summary>
    /// The cross is the shelf's to take back, the way the tick is: putting a row in date ticks the entry
    /// off again, and letting the count drop reopens it as work rather than leaving it crossed out.
    /// </summary>
    [Fact]
    public async Task The_cross_goes_when_the_row_is_replaced_and_the_entry_reopens_when_the_count_drops()
    {
        var shelfItem = await AShelfItemAsync(quantity: 4, minimumQuantity: 2, expiryDate: UseByDaysAgo(3));
        var entry = StandingFor(shelfItem);
        Assert.True(await CrossOffAsync(entry));
        Assert.True(entry.IsFailed);

        // A fresh one on the shelf, in date.
        shelfItem.Update(
            shelfItem.Name, shelfItem.ProductType, shelfItem.Categories, quantity: 4, minimumQuantity: 2,
            shelfItem.Unit, expiryDate: null, shelfItem.ExpiryNotificationChannel, shelfItem.IsCheckedRegularly);
        await _context.InventoryItemRepository.UpdateAsync(shelfItem, CancellationToken.None);

        Assert.True(await CrossOffAsync(entry));
        Assert.True(entry.IsCompleted);
        Assert.False(entry.IsFailed);

        shelfItem.MoveStockBy(-4);
        await _context.InventoryItemRepository.UpdateAsync(shelfItem, CancellationToken.None);

        Assert.True(await CrossOffAsync(entry));
        Assert.False(entry.IsResolved);
    }

    /// <summary>
    /// A tick somebody put there by hand is still theirs. The shelf crosses out what it settled itself,
    /// and nothing else - the same rule that keeps it from unticking a restock errand somebody has been
    /// out and done.
    /// </summary>
    [Fact]
    public async Task An_entry_somebody_ticked_themselves_is_not_crossed_out_by_a_date()
    {
        var shelfItem = await AShelfItemAsync(quantity: 4, minimumQuantity: 2, expiryDate: UseByDaysAgo(3));
        var entry = StandingFor(shelfItem, isCompleted: true);
        entry.RecordStock(TaskItemStock.Stocked);

        Assert.False(await CrossOffAsync(entry));

        Assert.True(entry.IsCompleted);
        Assert.False(entry.IsFailed);
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
                _context.StockedEntryCompletion,
                _context.StockedEntryStock,
                _context.ProductEntryPlacement)
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

    /// <summary>
    /// And back again, asked for on 2026-09-19: counting a product down past what the lists need puts
    /// the work back in front of the reader, the same way counting it up took it away. Only what the
    /// shelf crossed off itself - see the note at the top of StockedEntryCompletion about whose tick is
    /// whose.
    /// </summary>
    [Fact]
    public async Task What_the_shelf_crossed_off_it_reopens_once_it_no_longer_covers()
    {
        var shelfItem = await AShelfItemAsync(quantity: 4, minimumQuantity: 2);
        var entry = StandingFor(shelfItem);
        Assert.True(await CrossOffAsync(entry));

        // Counted back down on the shelf itself, which is what the + and - on an inventory do.
        shelfItem.MoveStockBy(-3);
        await _context.InventoryItemRepository.UpdateAsync(shelfItem, CancellationToken.None);

        Assert.True(await CrossOffAsync(entry));

        Assert.False(entry.IsCompleted);
    }

    /// <summary>
    /// A tick somebody put there by hand is theirs. It stays whatever the count does - taking it away
    /// because a number moved would be arguing with them, and on a restock list a crossed-off errand
    /// means "I have been", which is what fills the shelf in the first place.
    /// </summary>
    [Fact]
    public async Task A_tick_somebody_gave_by_hand_survives_the_count_dropping()
    {
        var shelfItem = await AShelfItemAsync(quantity: 0, minimumQuantity: 2);
        var entry = StandingFor(shelfItem, isCompleted: true);
        entry.RecordStock(TaskItemStock.Stocked);

        Assert.False(await CrossOffAsync(entry));

        Assert.True(entry.IsCompleted);
    }

    /// <summary>
    /// The same from the shelf's own end: saving an inventory with the count down again reopens what it
    /// had crossed off, and writes the list it is on - see UpdateInventoryCommandHandler.
    /// </summary>
    [Fact]
    public async Task Counting_a_product_down_on_the_shelf_puts_the_work_back_on_the_list()
    {
        var inventoryId = _context.AddInventory(_userId);
        var shelfItem = InventoryItem.Create(
            inventoryId, "Zupka Buldog", "Food", null, quantity: 4, minimumQuantity: 2,
            InventoryUnit.Piece, expiryDate: null, NotificationChannel.None);
        await _context.InventoryItemRepository.AddAsync(shelfItem, CancellationToken.None);
        var entry = StandingFor(shelfItem);
        var taskList = TaskList.Create(_userId, "Zakupy", [entry]);
        await _context.TaskRepository.AddAsync(taskList, CancellationToken.None);
        Assert.True(await CrossOffAsync(entry));
        await _context.TaskRepository.UpdateAsync(taskList, CancellationToken.None);

        await _context.InventorySave().HandleAsync(
            new UpdateInventoryCommand(
                _userId, inventoryId, "Kitchen",
                [
                    new InventoryItemInput(
                        shelfItem.Id, shelfItem.Name, shelfItem.ProductType, shelfItem.Categories,
                        Quantity: 1, MinimumQuantity: 2, shelfItem.Unit, shelfItem.ExpiryDate,
                        shelfItem.ExpiryNotificationChannel)
                ],
                IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);

        var stored = await _context.TaskRepository.GetByIdAsync(_userId, taskList.Id, CancellationToken.None);
        Assert.False(Assert.Single(stored!.Items).IsCompleted);
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
