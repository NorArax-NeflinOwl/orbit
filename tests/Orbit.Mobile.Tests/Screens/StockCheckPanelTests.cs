using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Inventory;
using Orbit.Contracts.Tasks;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Tasks;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// "Can this be done?" - what a group list's work costs against a warehouse. The arithmetic is the
/// server's; what this has to get right is which question it asks, and what it does with silence.
/// </summary>
public sealed class StockCheckPanelTests
{
    /// <summary>
    /// Any list can ask, not only a group one: a list holding errands about stock is asking the
    /// question whether or not it gathers other lists. Orbit.Web widened the same rule.
    /// </summary>
    [Fact]
    public async Task It_is_offered_whether_or_not_the_list_gathers_others()
    {
        using var context = new PanelContext();

        Assert.True((await context.ShowAsync(isGroup: false)).IsOffered);
        Assert.True((await context.ShowAsync(isGroup: true)).IsOffered);
    }

    /// <summary>
    /// A list the server has never seen cannot ask: the count is worked out there, against an id this
    /// phone has not got yet.
    /// </summary>
    [Fact]
    public async Task A_list_the_server_has_not_seen_cannot_ask()
    {
        using var context = new PanelContext();

        Assert.False((await context.ShowAsync(isGroup: true, hasReachedTheServer: false)).IsOffered);
    }

    /// <summary>
    /// No warehouse chosen is not an answer of "nothing" - there is no question yet, and saying
    /// "everything is on the shelf" about no shelf would be untrue.
    /// </summary>
    [Fact]
    public async Task Without_a_warehouse_there_is_no_answer_at_all()
    {
        using var context = new PanelContext();
        context.Server.StockCheck = new TaskListStockCheckDto(true, []);

        var panel = await context.ShowAsync(isGroup: true);

        Assert.Empty(panel.Summary);
        Assert.Empty(panel.Requirements);
    }

    [Fact]
    public async Task With_a_warehouse_it_says_what_is_short()
    {
        using var context = new PanelContext();
        var warehouse = await context.AddWarehouseAsync("Kitchen");
        context.Server.StockCheck = new TaskListStockCheckDto(
            false,
            [new StockRequirementDto("Flour", 3, 1, 2), new StockRequirementDto("Salt", 1, 1, 0)]);

        var panel = await context.ShowAsync(isGroup: true, linkedWarehouseId: warehouse);

        Assert.True(panel.IsShortOfSomething);
        Assert.Contains("1", panel.Summary);
        Assert.Equal(["Flour", "Salt"], panel.Requirements.Select(requirement => requirement.Name));
        // Only what falls short is written in the last column; a row that is covered says nothing.
        Assert.True(panel.Requirements[0].IsShort);
        Assert.False(panel.Requirements[1].IsShort);
    }

    /// <summary>
    /// The panel asks about a shelf, which is not what somebody working through the list is looking at -
    /// so it can be put away, and stays away for this list on this device. Orbit.Web folds it the same way.
    /// </summary>
    [Fact]
    public async Task Folding_it_away_is_remembered_for_that_list()
    {
        using var context = new PanelContext();
        var panel = await context.ShowAsync(isGroup: true);

        panel.ToggleFoldCommand.Execute(null);

        Assert.True(panel.IsFolded);
        Assert.False(panel.IsNotFolded);
        Assert.True((await context.ShowAsync(isGroup: true)).IsFolded);
    }

    /// <summary>Opening a list nobody has folded shows the panel: it is only away once put away.</summary>
    [Fact]
    public async Task It_opens_unfolded()
    {
        using var context = new PanelContext();

        Assert.False((await context.ShowAsync(isGroup: true)).IsFolded);
    }

    [Theory]
    [InlineData(StockCheckOrder.AsCounted, new[] { "Flour", "Salt", "Butter" })]
    [InlineData(StockCheckOrder.Alphabetical, new[] { "Butter", "Flour", "Salt" })]
    [InlineData(StockCheckOrder.ReverseAlphabetical, new[] { "Salt", "Flour", "Butter" })]
    // Shortfalls first, and within them the order the work asks for them in.
    [InlineData(StockCheckOrder.ShortFirst, new[] { "Flour", "Butter", "Salt" })]
    public async Task The_rows_are_read_in_the_chosen_order(StockCheckOrder order, string[] expected)
    {
        using var context = new PanelContext();
        var warehouse = await context.AddWarehouseAsync("Kitchen");
        context.Server.StockCheck = new TaskListStockCheckDto(
            false,
            [
                new StockRequirementDto("Flour", 3, 1, 2),
                new StockRequirementDto("Salt", 1, 1, 0),
                new StockRequirementDto("Butter", 2, 0, 2)
            ]);
        var panel = await context.ShowAsync(isGroup: true, linkedWarehouseId: warehouse);

        panel.Order = order;

        Assert.Equal(expected, panel.Requirements.Select(requirement => requirement.Name));
    }

    /// <summary>
    /// Sorted from the order the work was counted in rather than from what is on screen, so going
    /// through the orders and back leaves the rows where they started.
    /// </summary>
    [Fact]
    public async Task Going_back_to_the_list_order_gives_the_list_order()
    {
        using var context = new PanelContext();
        var warehouse = await context.AddWarehouseAsync("Kitchen");
        context.Server.StockCheck = new TaskListStockCheckDto(
            false, [new StockRequirementDto("Flour", 3, 1, 2), new StockRequirementDto("Butter", 2, 2, 0)]);
        var panel = await context.ShowAsync(isGroup: true, linkedWarehouseId: warehouse);

        panel.Order = StockCheckOrder.Alphabetical;
        panel.Order = StockCheckOrder.AsCounted;

        Assert.Equal(["Flour", "Butter"], panel.Requirements.Select(requirement => requirement.Name));
    }

    [Fact]
    public async Task The_chosen_order_is_remembered_for_that_list()
    {
        using var context = new PanelContext();
        var panel = await context.ShowAsync(isGroup: true);

        panel.Order = StockCheckOrder.ShortFirst;

        Assert.Equal(StockCheckOrder.ShortFirst, (await context.ShowAsync(isGroup: true)).Order);
    }

    [Fact]
    public async Task When_the_shelf_covers_it_there_is_nothing_to_raise()
    {
        using var context = new PanelContext();
        var warehouse = await context.AddWarehouseAsync("Kitchen");
        context.Server.StockCheck = new TaskListStockCheckDto(true, [new StockRequirementDto("Salt", 1, 4, 0)]);

        var panel = await context.ShowAsync(isGroup: true, linkedWarehouseId: warehouse);

        Assert.False(panel.IsShortOfSomething);
        Assert.NotEmpty(panel.Summary);
    }

    /// <summary>
    /// Recalculating used to only ask again. The web's has always applied the answer - crossing off what
    /// the shelf covers and writing on what it holds that nothing asked for - which left the phone's
    /// reader ticking off by hand what the panel had just told them was already there.
    /// </summary>
    /// <summary>
    /// What Orbit.Web's rebuild put in place of two half-actions: rebuild the list against what is on
    /// the shelves now. The phone was still offering one of the two it replaced.
    /// </summary>
    [Fact]
    public async Task Refreshing_rebuilds_the_list_against_the_warehouse()
    {
        using var context = new PanelContext();
        var warehouse = await context.AddWarehouseAsync("Kitchen");
        context.Server.StockCheck = new TaskListStockCheckDto(true, []);
        context.Warehouses.RestockRefresh = new RestockRefreshResultDto(AddedCount: 3, RemovedCount: 2);
        var panel = await context.ShowAsync(isGroup: true, linkedWarehouseId: warehouse);

        await panel.RefreshFromTheWarehouseCommand.ExecuteAsync(null);

        Assert.Equal(1, context.Warehouses.RestockRefreshesAsked);
        Assert.Contains("3", panel.Message);
        Assert.Contains("2", panel.Message);
    }

    [Fact]
    public async Task Refreshing_when_nothing_moved_says_the_list_already_asks_for_the_right_things()
    {
        using var context = new PanelContext();
        var warehouse = await context.AddWarehouseAsync("Kitchen");
        context.Server.StockCheck = new TaskListStockCheckDto(true, []);
        var panel = await context.ShowAsync(isGroup: true, linkedWarehouseId: warehouse);

        await panel.RefreshFromTheWarehouseCommand.ExecuteAsync(null);

        Assert.Equal(1, context.Warehouses.RestockRefreshesAsked);
        Assert.Contains("already asks", panel.Message);
    }

    /// <summary>A list measured against nothing has no warehouse to rebuild from, so nothing is asked.</summary>
    [Fact]
    public async Task A_list_measured_against_nothing_refreshes_nothing()
    {
        using var context = new PanelContext();
        await context.AddWarehouseAsync("Kitchen");
        context.Server.StockCheck = new TaskListStockCheckDto(true, []);
        var panel = await context.ShowAsync(isGroup: true);

        await panel.RefreshFromTheWarehouseCommand.ExecuteAsync(null);

        Assert.Equal(0, context.Warehouses.RestockRefreshesAsked);
    }

    /// <summary>
    /// The arithmetic is the server's and the shelf is not on this phone, so a stale answer about it
    /// would be worse than none - the panel says it could not ask rather than guessing.
    /// </summary>
    [Fact]
    public async Task Without_a_connection_it_says_so_rather_than_guessing()
    {
        using var context = new PanelContext();
        var warehouse = await context.AddWarehouseAsync("Kitchen");
        context.Server.IsUnreachable = true;

        var panel = await context.ShowAsync(isGroup: true, linkedWarehouseId: warehouse);

        Assert.NotEmpty(panel.Summary);
        Assert.Empty(panel.Requirements);
        Assert.False(panel.IsShortOfSomething);
    }

    [Fact]
    public async Task Raising_what_is_short_says_how_many_went_on_the_list()
    {
        using var context = new PanelContext();
        var warehouse = await context.AddWarehouseAsync("Kitchen");
        context.Server.StockCheck = new TaskListStockCheckDto(false, [new StockRequirementDto("Flour", 3, 1, 2)]);
        context.Server.RaisedShortfallCount = 2;

        var panel = await context.ShowAsync(isGroup: true, linkedWarehouseId: warehouse);
        await panel.RaiseShortfallsCommand.ExecuteAsync(null);

        Assert.Contains("2", panel.Message);
    }

    /// <summary>Nothing added is a real answer, not a failure - what is short may already be waiting.</summary>
    [Fact]
    public async Task Raising_nothing_says_that_too()
    {
        using var context = new PanelContext();
        var warehouse = await context.AddWarehouseAsync("Kitchen");
        context.Server.StockCheck = new TaskListStockCheckDto(false, [new StockRequirementDto("Flour", 3, 1, 2)]);
        context.Server.RaisedShortfallCount = 0;

        var panel = await context.ShowAsync(isGroup: true, linkedWarehouseId: warehouse);
        await panel.RaiseShortfallsCommand.ExecuteAsync(null);

        Assert.NotEmpty(panel.Message);
    }

    /// <summary>
    /// Building a shelf changes the list itself - it now points at a warehouse it did not have - so
    /// the screen has to be told to read it again.
    /// </summary>
    [Fact]
    public async Task Generating_a_warehouse_asks_the_screen_to_read_the_list_again()
    {
        using var context = new PanelContext();
        var panel = await context.ShowAsync(isGroup: true);
        var toldToReadAgain = false;
        panel.Changed += (_, _) => toldToReadAgain = true;

        await panel.GenerateInventoryCommand.ExecuteAsync(null);

        Assert.True(toldToReadAgain);
        Assert.NotEmpty(panel.Message);
    }

    [Fact]
    public async Task A_list_with_nothing_to_build_from_says_so()
    {
        using var context = new PanelContext();
        context.Server.GeneratedWarehouseId = null;
        var panel = await context.ShowAsync(isGroup: true);

        await panel.GenerateInventoryCommand.ExecuteAsync(null);

        Assert.NotEmpty(panel.Message);
    }

    /// <summary>"Not measured against a warehouse" leads the list, as it does on the web.</summary>
    [Fact]
    public async Task The_warehouses_offered_start_with_none()
    {
        using var context = new PanelContext();
        await context.AddWarehouseAsync("Kitchen");

        var panel = await context.ShowAsync(isGroup: true);

        Assert.Null(panel.Warehouses[0].ServerId);
        Assert.Equal(2, panel.Warehouses.Count);
    }

    private sealed class PanelContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-28T10:00:00Z"));
        private readonly LocalWarehouseRepository _warehouses;

        public PanelContext()
        {
            Server = new FakeTasksServer(_clock);
            _warehouses = new LocalWarehouseRepository(_localStore, _clock, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());
        }

        public FakeTasksServer Server { get; }

        /// <summary>The shelves themselves, which is where a refresh is asked for - see StockCheckPanel.</summary>
        public FakeInventoryServer Warehouses { get; } = new(TimeProvider.System);

        /// <summary>A warehouse the server knows about, which is what makes it choosable.</summary>
        public async Task<Guid> AddWarehouseAsync(string name)
        {
            var warehouse = await _warehouses.CreateAsync(name);
            await using var dbContext = _localStore.CreateDbContext();
            var stored = dbContext.Warehouses.Single(candidate => candidate.LocalId == warehouse.LocalId);
            stored.ServerId = Guid.NewGuid();
            await dbContext.SaveChangesAsync();
            return stored.ServerId.Value;
        }

        /// <summary>How this reader reads this list, kept across the panels one test opens.</summary>
        public InMemoryChecklistReadingStore Reading { get; } = new();

        /// <summary>
        /// The same list every time, so a test can close a panel and open another one on it and see what
        /// was remembered about it - see the folding.
        /// </summary>
        private readonly Guid _taskListLocalId = Guid.NewGuid();

        public async Task<StockCheckPanel> ShowAsync(
            bool isGroup, Guid? linkedWarehouseId = null, bool hasReachedTheServer = true)
        {
            var panel = new StockCheckPanel(
                new TasksClient(Server.ToHttpClient()), new InventoryClient(Warehouses.ToHttpClient()), _warehouses,
                new Translations(new InMemoryLanguageStore()), Connections.Online, Reading);

            await panel.ShowAsync(new LocalTaskList
            {
                LocalId = _taskListLocalId,
                ServerId = hasReachedTheServer ? Guid.NewGuid() : null,
                IsGroup = isGroup,
                LinkedWarehouseId = linkedWarehouseId
            });

            return panel;
        }

        public void Dispose()
        {
            Server.Dispose();
            Warehouses.Dispose();
            _localStore.Dispose();
        }
    }
}
