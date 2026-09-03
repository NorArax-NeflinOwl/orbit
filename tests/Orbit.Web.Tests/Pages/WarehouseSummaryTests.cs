using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Inventory;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// A shelf read rather than edited. A row is a batch - two rows can carry the same name, which is what
/// two deliveries of one thing are - and each says how much, when it arrived and how long it keeps.
/// </summary>
public sealed class WarehouseSummaryTests : OrbitTestContext
{
    private static readonly Guid WarehouseId = Guid.NewGuid();
    private static readonly Guid FirstBatchId = Guid.NewGuid();

    private IReadOnlyList<InventoryItemDto> _shelf = [];

    /// <summary>Whether the shelf refuses to be written, so a test can watch what the page says about it.</summary>
    private bool _savingFails;

    /// <summary>Whether this shelf is shared to this reader, and on what terms - see the read-only tests.</summary>
    private bool _isShared;
    private string _accessLevel = "CanEdit";

    public WarehouseSummaryTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
            request.Method == HttpMethod.Put && _savingFails
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = request.RequestUri!.AbsolutePath.EndsWith("/items", StringComparison.Ordinal)
                ? JsonContent.Create(_shelf)
                : JsonContent.Create(new WarehouseDto(
                    WarehouseId, "Pantry", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                    _isShared, SharedByUserName: _isShared ? "Anna" : null, _accessLevel, LockedByUserName: null,
                    OriginalOwnerUserId: null, Description: "What the kitchen keeps"))
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        Services.AddSingleton(new InventoryApiClient(httpClient));
    }

    [Fact]
    public void Every_batch_says_how_much_when_it_arrived_and_how_long_it_keeps()
    {
        _shelf = [
            Batch(FirstBatchId, "Flour", 2, new DateTime(2026, 8, 20), new DateTime(2026, 12, 1)),
            Batch(Guid.NewGuid(), "Flour", 1, new DateTime(2026, 9, 1), expires: null)];

        var cut = RenderComponent<WarehouseSummary>(parameters => parameters.Add(page => page.WarehouseId, WarehouseId));

        var rows = cut.FindAll(".shelf-batch").ToList();
        // Two rows for one name: two deliveries of the thing, not one row that has been counted twice.
        Assert.Equal(2, rows.Count);
        Assert.Contains("Flour", rows[0].TextContent);
        Assert.Contains("20.08.2026", rows[0].TextContent);
        Assert.Contains("01.12.2026", rows[0].TextContent);
        // Nothing said about keeping is a batch that keeps, rather than a blank where a date would be.
        Assert.Contains("keeps", rows[1].TextContent);
    }

    /// <summary>
    /// An errand about a product links here naming it - see TaskListChecklist's reference chips - so
    /// the row it meant is marked rather than left for the reader to find.
    /// </summary>
    [Fact]
    public void The_row_a_link_pointed_at_is_marked()
    {
        _shelf = [Batch(FirstBatchId, "Flour", 2, DateTime.Today, expires: null), Batch(Guid.NewGuid(), "Sugar", 1, DateTime.Today, null)];

        // Through the address bar, because that is where the link that means one puts it.
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"https://example.test/inventory/{WarehouseId}?highlight={FirstBatchId}");
        var cut = RenderComponent<WarehouseSummary>(parameters => parameters.Add(page => page.WarehouseId, WarehouseId));

        var marked = Assert.Single(cut.FindAll(".shelf-batch.highlighted"));
        Assert.Contains("Flour", marked.TextContent);
    }

    /// <summary>The fields behind the shelf are one press further in, and behind the menu beside Save.</summary>
    [Fact]
    public void Changing_what_is_on_the_shelf_is_a_named_press()
    {
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<WarehouseSummary>(parameters => parameters.Add(page => page.WarehouseId, WarehouseId));

        cut.Find(".editor-rail .overflow-menu-trigger").Click();
        cut.FindAll(".avatar-dropdown-item").First(entry => entry.TextContent.Trim() == "Edit").Click();

        Assert.EndsWith($"/inventory/{WarehouseId}/edit", navigationManager.Uri);
    }

    /// <summary>
    /// A share below CanEdit still opens the form - there is nothing else it could open - but nothing
    /// there can be saved, so the menu says "View" rather than promising a Save that will refuse. It
    /// also has nothing to delete: this warehouse is not this reader's to remove.
    /// </summary>
    [Fact]
    public void A_read_only_share_offers_View_and_no_Delete()
    {
        _isShared = true;
        _accessLevel = "ReadOnly";
        var cut = RenderComponent<WarehouseSummary>(parameters => parameters.Add(page => page.WarehouseId, WarehouseId));

        cut.Find(".editor-rail .overflow-menu-trigger").Click();
        var entries = cut.FindAll(".avatar-dropdown-item").Select(entry => entry.TextContent.Trim()).ToList();

        Assert.Contains("View", entries);
        Assert.DoesNotContain("Edit", entries);
        Assert.DoesNotContain("Delete", entries);
    }

    /// <summary>
    /// The two things somebody standing in front of a shelf does. Counted here rather than typed in an
    /// editor, and saved with one press - until then nothing has been written, which is what makes the
    /// pair safe to lean on.
    /// </summary>
    [Fact]
    public void One_off_the_shelf_and_one_back_on_it_are_a_press_each()
    {
        _shelf = [Batch(FirstBatchId, "Flour", 1, DateTime.Today, expires: null)];
        var cut = RenderComponent<WarehouseSummary>(parameters => parameters.Add(page => page.WarehouseId, WarehouseId));

        var save = cut.FindAll("button").First(button => button.GetAttribute("aria-label") == "Save");
        Assert.True(save.HasAttribute("disabled"));

        cut.FindAll(".shelf-batch-count button").First(button => button.TextContent.Contains('+')).Click();

        Assert.Contains("2", cut.Find(".shelf-batch-amount").TextContent);
        Assert.False(
            cut.FindAll("button").First(button => button.GetAttribute("aria-label") == "Save").HasAttribute("disabled"));
    }

    /// <summary>
    /// A save that did not happen is said beside the button that did not do it. A shelf is read by
    /// scrolling, so a sentence at the top of the page is one nobody scrolls back to - and "why has
    /// Save done nothing" is a question asked with the thumb still on Save.
    /// </summary>
    [Fact]
    public void A_save_that_failed_says_so_in_the_panel_that_stays()
    {
        _shelf = [Batch(FirstBatchId, "Flour", 1, DateTime.Today, expires: null)];
        _savingFails = true;
        var cut = RenderComponent<WarehouseSummary>(parameters => parameters.Add(page => page.WarehouseId, WarehouseId));

        cut.FindAll(".shelf-batch-count button").First(button => button.TextContent.Contains('+')).Click();
        cut.FindAll("button").First(button => button.GetAttribute("aria-label") == "Save").Click();

        Assert.Contains("Failed to save", cut.Find(".editor-rail-extras .error").TextContent);
    }

    /// <summary>
    /// Pressing the shelf opens the form that changes what is on it - the same rule a note's own page
    /// follows. Counting a batch is not that, and the two buttons that do it keep their own press.
    /// </summary>
    [Fact]
    public void Pressing_the_shelf_opens_the_form_and_counting_does_not()
    {
        _shelf = [Batch(FirstBatchId, "Flour", 1, DateTime.Today, expires: null)];
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<WarehouseSummary>(parameters => parameters.Add(page => page.WarehouseId, WarehouseId));
        var startedAt = navigationManager.Uri;

        cut.Find(".shelf-batch-count").Click();
        Assert.Equal(startedAt, navigationManager.Uri);

        cut.Find(".card").Click();

        Assert.EndsWith($"/inventory/{WarehouseId}/edit", navigationManager.Uri);
    }

    /// <summary>A shelf holding minus one of something is a number nobody can act on.</summary>
    [Fact]
    public void Nothing_on_the_shelf_cannot_be_counted_down_further()
    {
        _shelf = [Batch(FirstBatchId, "Flour", 0, DateTime.Today, expires: null)];

        var cut = RenderComponent<WarehouseSummary>(parameters => parameters.Add(page => page.WarehouseId, WarehouseId));

        var fewer = cut.FindAll(".shelf-batch-count button").First(button => button.TextContent.Contains('−'));
        Assert.True(fewer.HasAttribute("disabled"));
    }

    private static InventoryItemDto Batch(Guid id, string name, decimal quantity, DateTime added, DateTime? expires)
        => new(
            id, name, "Food", "Dry goods", quantity, MinimumQuantity: null, Unit: "Piece",
            ExpiryDate: expires is null ? null : new DateTimeOffset(DateTime.SpecifyKind(expires.Value, DateTimeKind.Local)),
            ExpiryNotificationChannel: "None", IsBelowMinimum: false, HasPendingRestockTask: false,
            CreatedAtUtc: new DateTimeOffset(DateTime.SpecifyKind(added, DateTimeKind.Local)),
            UpdatedAtUtc: DateTimeOffset.UtcNow);
}
