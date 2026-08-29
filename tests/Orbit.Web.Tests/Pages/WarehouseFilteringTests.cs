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

        var unitPicker = cut.Find(".editor-item-unit");
        var offered = unitPicker.QuerySelectorAll("option").Select(option => option.GetAttribute("value")).ToList();
        Assert.Equal(["Piece", "Kilogram", "Milligram", "Litre", "Millilitre", "Pack"], offered);
        Assert.Equal("Kilogram", unitPicker.GetAttribute("value"));
    }

    [Fact]
    public void A_unit_is_written_short_beside_the_amount()
    {
        // "2 kg" is what a shelf label says; "2 Kilogram" is not.
        RegisterApiClients([Item("Flour", unit: "Kilogram")]);

        var cut = Render();

        Assert.Contains("kg", cut.Find(".editor-item-unit").TextContent);
    }

    [Fact]
    public void A_changed_unit_is_what_gets_saved()
    {
        RegisterApiClients([Item("Flour", unit: "Piece")]);
        var cut = Render();

        cut.Find(".editor-item-unit").Change("Litre");
        ClickButtonSaying(cut, "Save");

        Assert.NotNull(_lastSavedJson);
        Assert.Contains("Litre", _lastSavedJson);
    }

    private IRenderedComponent<WarehouseEditor> Render()
        => RenderComponent<WarehouseEditor>(parameters => parameters.Add(editor => editor.WarehouseId, WarehouseId));

    private static void ClickButtonSaying(IRenderedComponent<WarehouseEditor> cut, string label)
        => cut.FindAll("button").First(button => button.TextContent.Contains(label, StringComparison.Ordinal)).Click();

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
