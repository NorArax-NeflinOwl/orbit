using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Inventory;
using Orbit.Contracts.Notifications;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// Narrowing a full shelf down to one type or one category, and what an item's amount is counted in.
/// The filter is a view and nothing else: what it hides is still saved.
/// </summary>
public sealed class WarehouseFilteringTests : OrbitTestContext
{
    private static readonly Guid WarehouseId = Guid.NewGuid();

    /// <summary>The body of the last save, so a test can ask what actually went back to the server.</summary>
    private string? _lastSavedJson;

    public WarehouseFilteringTests() => Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

    [Fact]
    public void The_filter_offers_the_types_and_categories_actually_on_the_shelf()
    {
        RegisterApiClients([
            Item("Flour", productType: "Food", category: "Dry"),
            Item("Bleach", productType: "Cleaning", category: "Under the sink")]);

        var cut = Render();

        var options = cut.FindAll(".inventory-filters option").Select(option => option.TextContent).ToList();
        Assert.Contains("Food", options);
        Assert.Contains("Cleaning", options);
        Assert.Contains("Under the sink", options);
        // Nothing is filed under this, so offering it would be a dead end.
        Assert.DoesNotContain("Frozen", options);
    }

    [Fact]
    public void Choosing_a_type_leaves_only_what_is_filed_under_it()
    {
        RegisterApiClients([
            Item("Flour", productType: "Food", category: "Dry"),
            Item("Sugar", productType: "Food", category: "Dry"),
            Item("Bleach", productType: "Cleaning", category: "Under the sink")]);
        var cut = Render();

        ChooseProductType(cut, "Cleaning");

        Assert.Equal(["Bleach"], ItemNamesIn(cut));
    }

    [Fact]
    public void Choosing_a_category_leaves_only_what_is_filed_under_it()
    {
        RegisterApiClients([
            Item("Flour", productType: "Food", category: "Dry"),
            Item("Milk", productType: "Food", category: "Fridge")]);
        var cut = Render();

        ChooseCategory(cut, "Fridge");

        Assert.Equal(["Milk"], ItemNamesIn(cut));
    }

    [Fact]
    public void Both_together_narrow_further_rather_than_wider()
    {
        RegisterApiClients([
            Item("Flour", productType: "Food", category: "Dry"),
            Item("Milk", productType: "Food", category: "Fridge"),
            Item("Bleach", productType: "Cleaning", category: "Fridge")]);
        var cut = Render();

        ChooseProductType(cut, "Food");
        ChooseCategory(cut, "Fridge");

        Assert.Equal(["Milk"], ItemNamesIn(cut));
    }

    [Fact]
    public void A_filter_that_matches_nothing_says_the_rest_is_still_there()
    {
        // An empty list under a filter reads as an empty warehouse, which is the one thing it is not.
        RegisterApiClients([
            Item("Flour", productType: "Food", category: "Dry"),
            Item("Bleach", productType: "Cleaning", category: "Under the sink")]);
        var cut = Render();

        ChooseProductType(cut, "Food");
        ChooseCategory(cut, "Under the sink");

        Assert.Empty(ItemNamesIn(cut));
        Assert.Contains("still there", cut.Markup);
    }

    [Fact]
    public void A_narrowed_shelf_says_how_much_of_it_is_showing()
    {
        RegisterApiClients([
            Item("Flour", productType: "Food", category: "Dry"),
            Item("Bleach", productType: "Cleaning", category: "Under the sink")]);
        var cut = Render();

        ChooseProductType(cut, "Food");

        Assert.Contains("Showing 1 of 2 items", cut.Markup);
    }

    [Fact]
    public void Saving_a_narrowed_shelf_keeps_everything_on_it()
    {
        // The one thing a filter must never do. It hides rows from the screen, not from the warehouse.
        RegisterApiClients([
            Item("Flour", productType: "Food", category: "Dry"),
            Item("Bleach", productType: "Cleaning", category: "Under the sink")]);
        var cut = Render();
        ChooseProductType(cut, "Food");

        ClickButtonSaying(cut, "Save");

        Assert.NotNull(_lastSavedJson);
        Assert.Contains("Flour", _lastSavedJson);
        Assert.Contains("Bleach", _lastSavedJson);
    }

    [Fact]
    public void Clearing_the_filter_brings_the_rest_back()
    {
        RegisterApiClients([
            Item("Flour", productType: "Food", category: "Dry"),
            Item("Bleach", productType: "Cleaning", category: "Under the sink")]);
        var cut = Render();
        ChooseProductType(cut, "Food");

        ClickButtonSaying(cut, "Show everything");

        Assert.Equal(["Flour", "Bleach"], ItemNamesIn(cut));
    }

    [Fact]
    public void Adding_an_item_steps_the_filter_aside()
    {
        // A new row is filed under nothing, so under a filter it would be added and hidden in the same
        // click - which reads as a button that does not work.
        RegisterApiClients([
            Item("Flour", productType: "Food", category: "Dry"),
            Item("Bleach", productType: "Cleaning", category: "Under the sink")]);
        var cut = Render();
        ChooseProductType(cut, "Food");

        ClickButtonSaying(cut, "Add item");

        Assert.Equal(["Flour", "Bleach", ""], ItemNamesIn(cut));
    }

    [Fact]
    public void Every_unit_is_on_offer_and_the_item_keeps_its_own()
    {
        RegisterApiClients([Item("Flour", unit: "Kilogram")]);

        var cut = Render();
        ExpandTheOnlyItem(cut);

        var unitPicker = cut.Find(".editor-item-unit");
        var offered = unitPicker.QuerySelectorAll("option").Select(option => option.GetAttribute("value")).ToList();
        Assert.Equal(["Piece", "Kilogram", "Milligram", "Litre", "Millilitre", "Pack"], offered);
        Assert.Equal("Kilogram", unitPicker.GetAttribute("value"));
    }

    [Fact]
    public void A_unit_is_written_short_beside_the_amount()
    {
        // "2 kg" is what a shelf label says; "2 Kilogram" is not. Read off the row itself, not the
        // picker: the unit is chosen behind the toggle and only reported here.
        RegisterApiClients([Item("Flour", unit: "Kilogram")]);

        var cut = Render();

        Assert.Contains("kg", cut.Find(".editor-item-unit-label").TextContent);
    }

    [Fact]
    public void A_changed_unit_is_what_gets_saved()
    {
        RegisterApiClients([Item("Flour", unit: "Piece")]);
        var cut = Render();
        ExpandTheOnlyItem(cut);

        cut.Find(".editor-item-unit").Change("Litre");
        ClickButtonSaying(cut, "Save");

        Assert.NotNull(_lastSavedJson);
        Assert.Contains("Litre", _lastSavedJson);
    }


    [Fact]
    public void An_item_carrying_no_unit_at_all_shows_and_saves_pieces()
    {
        // A private warehouse sealed before units existed: its rows come back with none. The picker used
        // to show pieces - the first option - while the row itself held nothing, and that nothing is
        // what the next save wrote back.
        RegisterApiClients([Item("Flour", unit: null!)]);
        var cut = Render();
        ExpandTheOnlyItem(cut);

        Assert.Equal("Piece", cut.Find(".editor-item-unit").GetAttribute("value"));

        ClickButtonSaying(cut, "Save");

        Assert.NotNull(_lastSavedJson);
        Assert.Contains("Piece", _lastSavedJson);
    }

    /// <summary>
    /// Opens the row's other settings. The unit, the expiry and Remove all live behind the toggle now:
    /// the row itself carries what somebody is reading down a shelf for - the name and the two numbers.
    /// </summary>
    private static void ExpandTheOnlyItem(IRenderedFragment cut) => cut.Find(".editor-item-toggle").Click();

    [Fact]
    public void A_name_can_be_searched_for()
    {
        RegisterApiClients([Item("Flour"), Item("Sugar"), Item("Salt")]);
        var cut = Render();

        SearchFor(cut, "sug");

        Assert.Equal(["Sugar"], ItemNamesIn(cut));
    }

    [Fact]
    public void The_name_is_matched_anywhere_in_it_and_whatever_the_case()
    {
        // A shelf holds "Flour, wheat" and "Wholemeal flour", and somebody typing "flour" means both.
        RegisterApiClients([Item("Flour, wheat"), Item("Wholemeal FLOUR"), Item("Sugar")]);
        var cut = Render();

        SearchFor(cut, "flour");

        Assert.Equal(["Flour, wheat", "Wholemeal FLOUR"], ItemNamesIn(cut));
    }

    [Fact]
    public void A_search_and_a_filter_narrow_together_rather_than_apart()
    {
        RegisterApiClients([
            Item("Flour", productType: "Food", category: "Dry"),
            Item("Flour cleaner", productType: "Cleaning", category: "Under the sink")]);
        var cut = Render();

        SearchFor(cut, "flour");
        ChooseProductType(cut, "Cleaning");

        Assert.Equal(["Flour cleaner"], ItemNamesIn(cut));
    }

    [Fact]
    public void A_name_nothing_matches_says_the_rest_is_still_there()
    {
        RegisterApiClients([Item("Flour"), Item("Sugar")]);
        var cut = Render();

        SearchFor(cut, "nothing like it");

        Assert.Empty(ItemNamesIn(cut));
        Assert.Contains("still there", cut.Markup);
    }

    [Fact]
    public void A_searched_shelf_says_how_much_of_it_is_showing()
    {
        RegisterApiClients([Item("Flour"), Item("Sugar")]);
        var cut = Render();

        SearchFor(cut, "flour");

        Assert.Contains("Showing 1 of 2 items", cut.Markup);
    }

    [Fact]
    public void Clearing_brings_back_what_the_search_hid()
    {
        RegisterApiClients([Item("Flour"), Item("Sugar")]);
        var cut = Render();
        SearchFor(cut, "flour");

        ClickButtonSaying(cut, "Show everything");

        Assert.Equal(["Flour", "Sugar"], ItemNamesIn(cut));
    }

    [Fact]
    public void Saving_a_searched_shelf_keeps_everything_on_it()
    {
        // The same rule the filters follow: it hides rows from the screen, not from the warehouse.
        RegisterApiClients([Item("Flour"), Item("Sugar")]);
        var cut = Render();
        SearchFor(cut, "flour");

        ClickButtonSaying(cut, "Save");

        Assert.NotNull(_lastSavedJson);
        Assert.Contains("Flour", _lastSavedJson);
        Assert.Contains("Sugar", _lastSavedJson);
    }

    [Fact]
    public void Adding_an_item_steps_the_search_aside_too()
    {
        // A new row has no name at all, so under a search it would be added and hidden in one click.
        RegisterApiClients([Item("Flour"), Item("Sugar")]);
        var cut = Render();
        SearchFor(cut, "flour");

        ClickButtonSaying(cut, "Add item");

        Assert.Equal(["Flour", "Sugar", ""], ItemNamesIn(cut));
    }

    [Fact]
    public void A_shelf_filed_under_nothing_can_still_be_searched()
    {
        // Neither dropdown is offered when nothing is filed under a type or a category - but every item
        // has a name, so the box that searches them is always there.
        RegisterApiClients([Item("Flour", productType: "", category: ""), Item("Sugar", productType: "", category: "")]);
        var cut = Render();

        Assert.Empty(cut.FindAll(".inventory-filters select"));

        SearchFor(cut, "flour");

        Assert.Equal(["Flour"], ItemNamesIn(cut));
    }

    private static void SearchFor(IRenderedComponent<WarehouseEditor> cut, string name)
        => cut.Find(".inventory-filters-search input").Input(name);

    [Fact]
    public void A_row_can_be_moved_without_a_mouse()
    {
        // Dragging needs one, and a finger raises no drag events at all - so the same move is a button.
        RegisterApiClients([Item("Flour"), Item("Sugar"), Item("Salt")]);
        var cut = Render();

        MoveRow(cut, row: 2, "Move up");

        Assert.Equal(["Flour", "Salt", "Sugar"], ItemNamesIn(cut));
    }

    [Fact]
    public void A_row_can_be_moved_down_as_well()
    {
        RegisterApiClients([Item("Flour"), Item("Sugar")]);
        var cut = Render();

        MoveRow(cut, row: 0, "Move down");

        Assert.Equal(["Sugar", "Flour"], ItemNamesIn(cut));
    }

    [Fact]
    public void The_move_that_would_fall_off_the_end_is_offered_but_greyed_out()
    {
        // Rather than absent: a button appearing and vanishing as a row travels is harder to follow.
        RegisterApiClients([Item("Flour"), Item("Sugar")]);
        var cut = Render();

        Assert.True(ButtonOn(cut, row: 0, "Move up").HasAttribute("disabled"));
        Assert.False(ButtonOn(cut, row: 0, "Move down").HasAttribute("disabled"));
        Assert.True(ButtonOn(cut, row: 1, "Move down").HasAttribute("disabled"));
    }

    [Fact]
    public void A_moved_row_is_what_gets_saved()
    {
        RegisterApiClients([Item("Flour"), Item("Sugar")]);
        var cut = Render();

        MoveRow(cut, row: 0, "Move down");
        ClickButtonSaying(cut, "Save");

        Assert.NotNull(_lastSavedJson);
        Assert.True(
            _lastSavedJson.IndexOf("Sugar", StringComparison.Ordinal)
                < _lastSavedJson.IndexOf("Flour", StringComparison.Ordinal),
            "The saved order should be the arranged one.");
    }

    private static void MoveRow(IRenderedComponent<WarehouseEditor> cut, int row, string which)
        => ButtonOn(cut, row, which).Click();

    private static AngleSharp.Dom.IElement ButtonOn(IRenderedComponent<WarehouseEditor> cut, int row, string which)
        // Skip-then-First rather than an indexer: bUnit's refreshable collection has no working one.
        => cut.FindAll(".editor-item").Skip(row).First().QuerySelectorAll("button")
            .First(button => button.GetAttribute("title") == which);
    private IRenderedComponent<WarehouseEditor> Render()
        => RenderComponent<WarehouseEditor>(parameters => parameters.Add(editor => editor.WarehouseId, WarehouseId));

    private static void ClickButtonSaying(IRenderedComponent<WarehouseEditor> cut, string label)
        => ButtonSaying(cut, label).Click();

    /// <summary>
    /// A button by what it says - its words, or the name it carries for a screen reader, since an
    /// editor's Save and Cancel are icons now (see EditorRail.razor). The screen-reader name is looked
    /// at first and matched whole: a page can hold both the editor's Save and a "Save settings" beside
    /// something else, and by their words alone the wrong one answers to "Save".
    /// </summary>
    private static AngleSharp.Dom.IElement ButtonSaying(IRenderedFragment cut, string label)
        => cut.FindAll("button").FirstOrDefault(button =>
               string.Equals(button.GetAttribute("aria-label"), label, StringComparison.Ordinal))
            ?? cut.FindAll("button").First(button => button.TextContent.Contains(label, StringComparison.Ordinal));

    private static void ChooseProductType(IRenderedComponent<WarehouseEditor> cut, string productType)
        => cut.FindAll(".inventory-filters select").First().Change(productType);

    private static void ChooseCategory(IRenderedComponent<WarehouseEditor> cut, string category)
        => cut.FindAll(".inventory-filters select").Skip(1).First().Change(category);

    /// <summary>What each visible row's name box holds, in the order the rows are rendered.</summary>
    private static IReadOnlyList<string> ItemNamesIn(IRenderedComponent<WarehouseEditor> cut)
        => [.. cut.FindAll(".editor-item-main").Select(box => box.GetAttribute("value") ?? "")];

    private void RegisterApiClients(IReadOnlyList<InventoryItemDto> items)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (request.Method == HttpMethod.Put && request.Content is { } body)
            {
                _lastSavedJson = body.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path.EndsWith("/settings", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new NotificationSettingsDto(
                        true, true, true, true, ShowExceptionDetails: false,
                        BannerVisibleSeconds: 5, BannerMinimumGapSeconds: 5))
                };
            }

            if (path.EndsWith("/items", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(items) };
            }

            // ShareLinkButton asks on render whether this warehouse already has a public link, and the
            // lock is taken when the editor opens. NoContent answers both with "nothing to report".
            // Which lists are measured against this warehouse, and what the other warehouses are called
            // - none of these tests are about either, and the editor draws its checklist empty rather
            // than failing to open.
            if (path is "/api/tasks" or "/api/warehouses")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) };
            }

            if (path.EndsWith("/lock", StringComparison.Ordinal) || path.StartsWith("/api/share-links", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new WarehouseDto(
                    WarehouseId, "Pantry", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                    IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit",
                    LockedByUserName: null, OriginalOwnerUserId: null))
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new InventoryApiClient(httpClient));
        // The editor also asks which lists are measured against this warehouse - see its checklist.
        Services.AddSingleton(new TasksApiClient(httpClient));
        Services.AddSingleton(new NotificationsApiClient(httpClient));
        Services.AddSingleton(new PublicShareApiClient(httpClient));
    }

    private static InventoryItemDto Item(
        string name, string productType = "Food", string category = "Dry", string unit = "Piece")
        => new(
            Guid.NewGuid(), name, productType, category, Quantity: 1, MinimumQuantity: null, unit,
            ExpiryDate: null, ExpiryNotificationChannel: "None", IsBelowMinimum: false,
            HasPendingRestockTask: false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
