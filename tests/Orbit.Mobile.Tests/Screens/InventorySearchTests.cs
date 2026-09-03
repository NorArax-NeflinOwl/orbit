using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Inventories;
using Orbit.Core.Inventories;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Inventory;
using Orbit.Mobile.Security;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// "Which inventory is the flour in?" - the one question the inventory screen could not answer, because
/// it lists shelves and not what is on them. The shelf's own search box is one level below this.
/// </summary>
public sealed class InventorySearchTests
{
    [Fact]
    public async Task Searching_says_which_inventory_holds_it()
    {
        using var context = new ScreenContext();
        await context.AddInventoryAsync("Kitchen", Item("Flour"));
        await context.AddInventoryAsync("Workshop", Item("Wood glue"));
        var screen = await context.OpenInventoryAsync();

        screen.SearchedItemName = "flour";

        var match = Assert.Single(screen.ItemMatches);
        Assert.Equal("Flour", match.Name);
        Assert.Equal("Kitchen", match.InventoryName);
    }

    /// <summary>
    /// The same rule the shelf's own box follows: anywhere in the name and regardless of case, because a
    /// shelf holds "Flour, wheat" and "Wholemeal flour" and somebody typing "flour" means both.
    /// </summary>
    [Fact]
    public async Task It_matches_anywhere_in_the_name_across_every_inventory()
    {
        using var context = new ScreenContext();
        await context.AddInventoryAsync("Kitchen", Item("Flour, wheat"));
        await context.AddInventoryAsync("Pantry", Item("Wholemeal flour"), Item("Sugar"));
        var screen = await context.OpenInventoryAsync();

        screen.SearchedItemName = "FLOUR";

        Assert.Equal(2, screen.ItemMatches.Count);
        Assert.DoesNotContain(screen.ItemMatches, match => match.Name == "Sugar");
    }

    /// <summary>Half the answer to "where is it" is "and is there any left".</summary>
    [Fact]
    public async Task A_result_says_how_much_of_it_there_is()
    {
        using var context = new ScreenContext();
        await context.AddInventoryAsync("Kitchen", Item("Milk", quantity: 2, unit: nameof(InventoryUnit.Litre)));
        var screen = await context.OpenInventoryAsync();

        screen.SearchedItemName = "milk";

        Assert.Contains("2", Assert.Single(screen.ItemMatches).Amount);
    }

    /// <summary>
    /// Opening a result opens the inventory holding it, which is the whole point: the answer to "where is
    /// it" has to be somewhere you can go.
    /// </summary>
    [Fact]
    public async Task Opening_a_result_opens_the_inventory_holding_it()
    {
        using var context = new ScreenContext();
        await context.AddInventoryAsync("Kitchen", Item("Sugar"));
        var workshop = await context.AddInventoryAsync("Workshop", Item("Flour paste"));
        var screen = await context.OpenInventoryAsync();
        screen.SearchedItemName = "paste";

        screen.OpenMatchCommand.Execute(Assert.Single(screen.ItemMatches));

        Assert.Equal(workshop.LocalId, context.Navigator.LastInventoryId);
    }

    /// <summary>
    /// And on the thing that was found, not just on the shelf holding it: a search across every
    /// inventory that leaves somebody looking for it again has answered half the question.
    /// </summary>
    [Fact]
    public async Task Opening_a_result_lands_on_the_thing_that_was_found()
    {
        using var context = new ScreenContext();
        var paste = Item("Flour paste");
        await context.AddInventoryAsync("Workshop", Item("Sugar"), paste);
        var screen = await context.OpenInventoryAsync();
        screen.SearchedItemName = "paste";

        screen.OpenMatchCommand.Execute(Assert.Single(screen.ItemMatches));

        Assert.Equal(paste.Id, context.Navigator.LastPointedAtProductId);
    }

    /// <summary>
    /// A sealed inventory is counted rather than skipped. Its items never came down to this phone, so a
    /// search that stayed quiet about it would answer "it is nowhere" when the truth is "I could not look
    /// there" - the one answer a search must never give by accident.
    ///
    /// Counted rather than named, which is what it used to be: a private inventory's name is sealed
    /// with the rest of it, so the names put in that sentence were the empty strings the server sends.
    /// </summary>
    [Fact]
    public async Task A_inventory_this_phone_cannot_open_is_counted_rather_than_ignored()
    {
        using var context = new ScreenContext();
        await context.AddInventoryAsync("Kitchen", Item("Flour"));
        await context.AddSealedInventoryAsync("Locked away");
        var screen = await context.OpenInventoryAsync();

        screen.SearchedItemName = "flour";

        Assert.Contains("could not be opened", screen.ItemMatchSummary);
        Assert.DoesNotContain("Locked away", screen.ItemMatchSummary);
    }

    /// <summary>Nothing to apologise for when every shelf could be read - just what was found.</summary>
    [Fact]
    public async Task With_every_shelf_readable_the_summary_is_only_the_count()
    {
        using var context = new ScreenContext();
        await context.AddInventoryAsync("Kitchen", Item("Flour"));
        await context.AddInventoryAsync("Pantry", Item("Flour, rye"));
        var screen = await context.OpenInventoryAsync();

        screen.SearchedItemName = "flour";

        Assert.Equal("Found in 2 of 2 inventories.", screen.ItemMatchSummary);
    }

    /// <summary>
    /// The shelf list steps aside while a search is on and comes back when it is cleared: a search is
    /// asked instead of reading the shelves, not as well as.
    /// </summary>
    [Fact]
    public async Task Clearing_the_search_brings_the_shelves_back()
    {
        using var context = new ScreenContext();
        await context.AddInventoryAsync("Kitchen", Item("Flour"));
        var screen = await context.OpenInventoryAsync();
        screen.SearchedItemName = "flour";
        Assert.False(screen.IsShowingInventories);

        screen.ClearItemSearchCommand.Execute(null);

        Assert.True(screen.IsShowingInventories);
        Assert.Empty(screen.ItemMatches);
        Assert.Single(screen.Inventories);
    }

    /// <summary>Whitespace is not a search, so a stray space does not hide every shelf on the screen.</summary>
    [Fact]
    public async Task A_box_holding_only_spaces_is_not_a_search()
    {
        using var context = new ScreenContext();
        await context.AddInventoryAsync("Kitchen", Item("Flour"));
        var screen = await context.OpenInventoryAsync();

        screen.SearchedItemName = "   ";

        Assert.True(screen.IsShowingInventories);
        Assert.Empty(screen.ItemMatches);
    }

    /// <summary>A search that found nothing says so, rather than showing an empty screen.</summary>
    [Fact]
    public async Task A_search_that_finds_nothing_says_so()
    {
        using var context = new ScreenContext();
        await context.AddInventoryAsync("Kitchen", Item("Flour"));
        var screen = await context.OpenInventoryAsync();

        screen.SearchedItemName = "screwdriver";

        Assert.True(screen.FoundNothing);
    }

    private static InventoryItemRequest Item(
        string name, decimal quantity = 1, string unit = nameof(InventoryUnit.Piece))
        => new(Guid.NewGuid(), name, string.Empty, string.Empty, quantity, null, unit, null, "None");

    private sealed class ScreenContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-30T10:00:00Z"));
        private readonly LocalInventoryRepository _inventories;
        private readonly InventorySynchronizer _synchronizer;
        private readonly FakeInventoryServer _server;

        public ScreenContext()
        {
            _server = new FakeInventoryServer(_clock);
            _inventories = new LocalInventoryRepository(_localStore, _clock, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());
            _synchronizer = new InventorySynchronizer(
                _localStore, new InventoryClient(_server.ToHttpClient()), _clock, new SyncGate(),
                NullLogger<InventorySynchronizer>.Instance);
        }

        public RecordingScreenNavigator Navigator { get; } = new();

        /// <summary>An inventory is created empty, so its items are put in by the same update a screen makes.</summary>
        public async Task<LocalInventory> AddInventoryAsync(string name, params InventoryItemRequest[] items)
        {
            var inventory = await _inventories.CreateAsync(name);
            await _inventories.UpdateAsync(inventory.LocalId, new InventoryContent(name, items));
            return inventory;
        }

        /// <summary>A shelf sealed with a key this phone has not got, as the sync would bring one down.</summary>
        public async Task AddSealedInventoryAsync(string name)
        {
            var inventory = await _inventories.CreateAsync(name);
            await using var dbContext = _localStore.CreateDbContext();
            dbContext.Inventories.Single(stored => stored.LocalId == inventory.LocalId).IsPrivate = true;
            await dbContext.SaveChangesAsync();
        }

        public async Task<InventoryViewModel> OpenInventoryAsync()
        {
            var translations = new Translations(new InMemoryLanguageStore());
            var screen = new InventoryViewModel(
                _inventories, _synchronizer, FixedNetworkStatus.Online,
                new PrivateItemGate(new FixedDeviceAuthentication()),
                new SyncState(FixedNetworkStatus.Online, _clock), Navigator, translations);

            await screen.LoadCommand.ExecuteAsync(null);
            return screen;
        }

        public void Dispose()
        {
            _server.Dispose();
            _localStore.Dispose();
        }
    }
}
