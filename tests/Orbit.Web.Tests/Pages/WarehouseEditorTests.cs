using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Inventory;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Notifications;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Orbit.Web.Tests;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// Covers what an inventory row tells you at a glance: how much of something there is, and whether that
/// is little enough to do something about.
/// </summary>
public sealed class WarehouseEditorTests : OrbitTestContext
{
    private static readonly Guid WarehouseId = Guid.NewGuid();

    public WarehouseEditorTests() => Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

    [Fact]
    public void An_amount_is_called_an_amount()
    {
        RegisterApiClients([Item("Flour", quantity: 3)]);

        var cut = RenderComponent<WarehouseEditor>(parameters => parameters.Add(editor => editor.WarehouseId, WarehouseId));

        Assert.Contains("Amount", cut.Markup);
        Assert.DoesNotContain("Qty", cut.Markup);
    }

    [Fact]
    public void An_item_below_its_minimum_is_flagged()
    {
        RegisterApiClients([Item("Flour", quantity: 1, minimum: 5)]);

        var cut = RenderComponent<WarehouseEditor>(parameters => parameters.Add(editor => editor.WarehouseId, WarehouseId));

        Assert.Single(cut.FindAll(".item-warning"));
    }

    [Fact]
    public void An_item_sitting_exactly_on_its_minimum_is_not_flagged()
    {
        // The same line the restock task is raised on. Flagging here while no task had been raised
        // would read as the app disagreeing with itself.
        RegisterApiClients([Item("Flour", quantity: 5, minimum: 5)]);

        var cut = RenderComponent<WarehouseEditor>(parameters => parameters.Add(editor => editor.WarehouseId, WarehouseId));

        Assert.Empty(cut.FindAll(".item-warning"));
    }

    [Fact]
    public void A_hair_under_the_minimum_is_flagged()
    {
        // The boundary from the other side, so "not flagged at the minimum" can't be satisfied by never
        // flagging anything near it.
        RegisterApiClients([Item("Flour", quantity: 4.99m, minimum: 5)]);

        var cut = RenderComponent<WarehouseEditor>(parameters => parameters.Add(editor => editor.WarehouseId, WarehouseId));

        Assert.Single(cut.FindAll(".item-warning"));
    }

    [Fact]
    public void An_item_with_plenty_left_is_not_flagged()
    {
        RegisterApiClients([Item("Flour", quantity: 10, minimum: 5)]);

        var cut = RenderComponent<WarehouseEditor>(parameters => parameters.Add(editor => editor.WarehouseId, WarehouseId));

        Assert.Empty(cut.FindAll(".item-warning"));
    }

    [Fact]
    public void An_item_with_no_minimum_set_is_never_flagged()
    {
        // Nothing has been said about how much is too little, so there is nothing to warn against -
        // including at zero, which without a minimum is just an item nobody has stocked yet.
        RegisterApiClients([Item("Flour", quantity: 0, minimum: null)]);

        var cut = RenderComponent<WarehouseEditor>(parameters => parameters.Add(editor => editor.WarehouseId, WarehouseId));

        Assert.Empty(cut.FindAll(".item-warning"));
    }

    [Fact]
    public void Only_the_items_that_are_low_are_flagged()
    {
        RegisterApiClients([
            Item("Flour", quantity: 1, minimum: 5),
            Item("Sugar", quantity: 10, minimum: 5),
            Item("Salt", quantity: 2, minimum: 2),
            Item("Pepper", quantity: 0, minimum: 1)]);

        var cut = RenderComponent<WarehouseEditor>(parameters => parameters.Add(editor => editor.WarehouseId, WarehouseId));

        // Flour and Pepper are under; Sugar has plenty and Salt is exactly on its minimum.
        Assert.Equal(2, cut.FindAll(".item-warning").Count);
    }

    [Fact]
    public void An_item_dragged_onto_another_takes_its_place()
    {
        // The shelf is saved in the order its rows are written in - see InventoryItem.Position - so
        // arranging them here is what the warehouse is read back in.
        RegisterApiClients([Item("Flour", quantity: 1), Item("Sugar", quantity: 1), Item("Salt", quantity: 1)]);
        var cut = RenderComponent<WarehouseEditor>(parameters => parameters.Add(editor => editor.WarehouseId, WarehouseId));

        cut.FindAll(".drag-handle").ToArray()[2].DragStart();
        cut.FindAll(".editor-item").ToArray()[0].Drop();

        Assert.Equal(["Salt", "Flour", "Sugar"], ItemNamesIn(cut));
    }

    [Fact]
    public void An_item_dropped_where_it_already_was_stays_put()
    {
        RegisterApiClients([Item("Flour", quantity: 1), Item("Sugar", quantity: 1)]);
        var cut = RenderComponent<WarehouseEditor>(parameters => parameters.Add(editor => editor.WarehouseId, WarehouseId));

        cut.FindAll(".drag-handle").ToArray()[0].DragStart();
        cut.FindAll(".editor-item").ToArray()[0].Drop();

        Assert.Equal(["Flour", "Sugar"], ItemNamesIn(cut));
    }

    /// <summary>What each row's name box holds, in the order the rows are rendered.</summary>
    private static IReadOnlyList<string> ItemNamesIn(IRenderedComponent<WarehouseEditor> cut)
        => [.. cut.FindAll(".editor-item-main").Select(box => box.GetAttribute("value") ?? "")];

    private void RegisterApiClients(IReadOnlyList<InventoryItemDto> items)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

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

    private static InventoryItemDto Item(string name, decimal quantity, decimal? minimum = null)
        => new(
            Guid.NewGuid(), name, "Food", "Dry", quantity, minimum, Unit: "Piece", ExpiryDate: null,
            ExpiryNotificationChannel: "None", IsBelowMinimum: minimum is { } value && quantity < value,
            HasPendingRestockTask: false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    /// <summary>
    /// A warehouse being made rather than one being read - "/inventory/new" rather than
    /// "/inventory/{id}/edit". Nothing here exists yet, so nothing that depends on it doing so is
    /// offered - see WarehouseEditor's own "!IsNewWarehouse" sections.
    /// </summary>
    [Fact]
    public void A_new_warehouse_offers_nothing_that_needs_one_to_exist_first()
    {
        RegisterApiClientsForANewWarehouse();

        var cut = RenderComponent<WarehouseEditor>();

        Assert.Contains("New warehouse", cut.Find("h1").TextContent);
        Assert.DoesNotContain("Lists measured against this warehouse", cut.Markup);
        Assert.DoesNotContain("Restock list", cut.Markup);
        Assert.Empty(cut.FindAll(".share-link"));
    }

    [Fact]
    public void A_new_warehouse_cannot_be_saved_before_it_has_a_name()
    {
        RegisterApiClientsForANewWarehouse();
        var cut = RenderComponent<WarehouseEditor>();

        var save = cut.FindAll("button").First(button => button.GetAttribute("aria-label") == "Save");
        Assert.True(save.HasAttribute("disabled"));
    }

    /// <summary>Saving one for the first time creates it and returns to the list, the same place saving an existing one leaves from.</summary>
    [Fact]
    public void Saving_a_new_warehouse_creates_it_and_returns_to_the_list()
    {
        RegisterApiClientsForANewWarehouse();
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<WarehouseEditor>();

        WriteTheName(cut, "Cellar");
        cut.FindAll("button").First(button => button.GetAttribute("aria-label") == "Save").Click();

        Assert.EndsWith("/inventory", navigationManager.Uri);
    }

    /// <summary>
    /// Types the warehouse's name. The field is a contenteditable driven by JS, which bUnit cannot type
    /// into, so this raises the same callback that JS raises after an edit - mirrors NoteEditorTests'
    /// WriteFirstLine.
    /// </summary>
    private static void WriteTheName(IRenderedComponent<WarehouseEditor> cut, string name)
    {
        var editor = cut.FindComponent<Web.Components.ChecklistTextEditor>();
        cut.InvokeAsync(() => editor.Instance.LinesChanged.InvokeAsync(
            [new NoteContentLineDto(name, IsChecklistItem: false, IsChecked: false)])).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Everything "/inventory/new" asks for before Save is pressed: notification settings, for the
    /// item form's channel options. Nothing else - see OnInitializedAsync's early return for a new
    /// warehouse - so a request this does not expect would mean a "!IsNewWarehouse" guard had gone
    /// missing somewhere.
    /// </summary>
    private void RegisterApiClientsForANewWarehouse()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/settings", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new NotificationSettingsDto(
                        true, true, true, true, ShowExceptionDetails: false,
                        BannerVisibleSeconds: 5, BannerMinimumGapSeconds: 5))
                };
            }

            if (request.Method == HttpMethod.Post && path == "/api/warehouses")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Guid.NewGuid()) };
            }

            throw new InvalidOperationException($"Unexpected request for a new warehouse: {request.Method} {path}");
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new InventoryApiClient(httpClient));
        Services.AddSingleton(new TasksApiClient(httpClient));
        Services.AddSingleton(new NotificationsApiClient(httpClient));
        Services.AddSingleton(new PublicShareApiClient(httpClient));
    }
}
