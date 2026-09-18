using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Inventories;
using Orbit.Contracts.Notifications;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// The shelf answering back to the lists. The user's rule, 2026-09-18: a change on a list updates the
/// inventory and a change on the inventory updates the list - and where one item is asked for by more
/// than one list, warn, because only the reader knows which of them actually changed. See ShelfDemand.
/// </summary>
public sealed class InventoryShelfDemandTests : OrbitTestContext
{
    private static readonly Guid InventoryId = Guid.NewGuid();
    private static readonly Guid FlourId = Guid.NewGuid();

    /// <summary>What the last save sent, so a test can read the body rather than only the outcome.</summary>
    private string? _lastSaveBody;

    public InventoryShelfDemandTests() => Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

    [Fact]
    public void A_row_says_which_lists_ask_for_it()
    {
        RegisterApiClients(AskedForBy("Bread", "Pizza"), usage: 3);

        var cut = RenderComponent<InventoryEditor>(parameters => parameters.Add(editor => editor.InventoryId, InventoryId));

        Assert.Contains("Bread, Pizza ask for 3 of this", cut.Markup);
    }

    /// <summary>
    /// One list asking is no question at all: that entry is the whole of the demand, so the new amount
    /// goes straight to it and the save happens on the press.
    /// </summary>
    [Fact]
    public void A_row_only_one_list_asks_for_saves_without_a_word()
    {
        RegisterApiClients(AskedForBy("Bread"), usage: 2);
        var cut = RenderComponent<InventoryEditor>(parameters => parameters.Add(editor => editor.InventoryId, InventoryId));

        WriteTheMinimum(cut, "6");
        Save(cut);

        Assert.Empty(cut.FindAll(".dialog-panel"));
        Assert.NotNull(_lastSaveBody);
    }

    [Fact]
    public void Changing_an_amount_two_lists_ask_for_stops_to_ask_which()
    {
        RegisterApiClients(AskedForBy("Bread", "Pizza"), usage: 3);
        var cut = RenderComponent<InventoryEditor>(parameters => parameters.Add(editor => editor.InventoryId, InventoryId));

        WriteTheMinimum(cut, "9");
        Save(cut);

        var panel = cut.Find(".dialog-panel");
        Assert.Contains("More than one list asks for this", panel.TextContent);
        Assert.Contains("Bread, Pizza", panel.TextContent);
        // And nothing was written until it is answered.
        Assert.Null(_lastSaveBody);
    }

    [Fact]
    public void Split_evenly_tells_the_server_to_divide_it()
    {
        RegisterApiClients(AskedForBy("Bread", "Pizza"), usage: 3);
        var cut = RenderComponent<InventoryEditor>(parameters => parameters.Add(editor => editor.InventoryId, InventoryId));

        WriteTheMinimum(cut, "9");
        Save(cut);
        cut.FindAll(".dialog-footer button").First(button => button.TextContent.Trim() == "Split evenly").Click();

        Assert.NotNull(_lastSaveBody);
        Assert.Contains(FlourId.ToString(), _lastSaveBody);
        Assert.Contains("\"splitEvenlyAcross\":[", _lastSaveBody);
    }

    /// <summary>
    /// The other answer: the shelf still saves - the amount they typed is what they meant for the shelf -
    /// and the lists are left for the reader to change themselves.
    /// </summary>
    [Fact]
    public void Changing_the_lists_yourself_saves_the_shelf_and_leaves_them_alone()
    {
        RegisterApiClients(AskedForBy("Bread", "Pizza"), usage: 3);
        var cut = RenderComponent<InventoryEditor>(parameters => parameters.Add(editor => editor.InventoryId, InventoryId));

        WriteTheMinimum(cut, "9");
        Save(cut);
        cut.FindAll(".dialog-footer button").First(button => button.TextContent.Trim() == "I'll change the lists myself").Click();

        Assert.NotNull(_lastSaveBody);
        Assert.Contains("\"splitEvenlyAcross\":[]", _lastSaveBody);
    }

    /// <summary>
    /// Nothing was asked because nothing moved. Opening a shelf, correcting how much of something there
    /// is and saving must not rewrite amounts across the lists - see InventoryEditor.RowsWhoseMinimumMoved.
    /// </summary>
    [Fact]
    public void Saving_without_touching_the_minimum_asks_nothing()
    {
        RegisterApiClients(AskedForBy("Bread", "Pizza"), usage: 3);
        var cut = RenderComponent<InventoryEditor>(parameters => parameters.Add(editor => editor.InventoryId, InventoryId));

        Save(cut);

        Assert.Empty(cut.FindAll(".dialog-panel"));
        Assert.NotNull(_lastSaveBody);
    }

    private static IReadOnlyList<ShelfClaimDto> AskedForBy(params string[] taskListNames)
        => [.. taskListNames.Select(name => new ShelfClaimDto(
            FlourId, Guid.NewGuid(), name, Guid.NewGuid(), "Flour", Quantity: 1))];

    /// <summary>
    /// Types into the row's second number box - Amount is the first, Min the second. The pair is what
    /// the row shows without being expanded, so their order is what the markup itself decides.
    /// </summary>
    private static void WriteTheMinimum(IRenderedComponent<InventoryEditor> cut, string minimum)
        => cut.FindAll(".editor-item-aside input[type=number]").ToList()[1].Change(minimum);

    private static void Save(IRenderedComponent<InventoryEditor> cut)
        => cut.FindAll("button").First(button => button.GetAttribute("aria-label") == "Save").Click();

    private void RegisterApiClients(IReadOnlyList<ShelfClaimDto> demand, decimal usage)
    {
        var items = new List<InventoryItemDto>
        {
            new(
                FlourId, "Flour", "Food", "Dry", Quantity: 1, MinimumQuantity: 2, Unit: "Kilogram", ExpiryDate: null,
                ExpiryNotificationChannel: "None", IsBelowMinimum: true, HasPendingRestockTask: false,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, IsCheckedRegularly: false, Categories: ["Dry"],
                Usage: usage)
        };

        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (request.Method == HttpMethod.Put && path == $"/api/inventories/{InventoryId}")
            {
                _lastSaveBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
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

            if (path.EndsWith("/demand", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(demand) };
            }

            if (path is "/api/tasks" or "/api/inventories")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) };
            }

            if (path.EndsWith("/lock", StringComparison.Ordinal) || path.StartsWith("/api/share-links", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new InventoryDto(
                    InventoryId, "Pantry", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                    IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit",
                    LockedByUserName: null, OriginalOwnerUserId: null))
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new InventoryApiClient(httpClient));
        Services.AddSingleton(new TasksApiClient(httpClient));
        Services.AddSingleton(new NotificationsApiClient(httpClient));
        Services.AddSingleton(new PublicShareApiClient(httpClient));
    }
}
