using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Inventories;
using Orbit.Contracts.Notifications;
using Orbit.Contracts.Tasks;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// A shelf that gathers other shelves, in the browser. Asked for on 2026-09-18: one entry on the list of
/// inventories, holding smaller inventories inside it, each able to answer to a different task list.
///
/// Two of the three places show it here - the group's own page, and the editor where the membership is
/// arranged. The card on the list of inventories is in InventoriesTests, which already stands that page
/// up.
/// </summary>
public sealed class GroupInventoryPagesTests : OrbitTestContext
{
    private static readonly Guid KitchenId = Guid.NewGuid();
    private static readonly Guid FridgeId = Guid.NewGuid();
    private static readonly Guid PantryId = Guid.NewGuid();

    /// <summary>What the page last asked the server to gather, and for which shelf.</summary>
    private (Guid InventoryId, IReadOnlyList<Guid> Members)? _gathered;

    /// <summary>What each inventory holds, by id - empty unless a test puts something on a shelf.</summary>
    private readonly Dictionary<Guid, IReadOnlyList<InventoryItemDto>> _shelves = [];

    private IReadOnlyList<Guid> _kitchenGathers = [];

    public GroupInventoryPagesTests() => Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

    /// <summary>The division, on the group's own page: each smaller shelf under its own name.</summary>
    [Fact]
    public void The_groups_own_page_draws_each_smaller_shelf_under_its_name()
    {
        _kitchenGathers = [FridgeId];
        _shelves[FridgeId] = [Batch("Milk", 2)];
        _shelves[KitchenId] = [Batch("Flour", 1)];
        RegisterApiClients();

        var cut = RenderComponent<InventorySummary>(
            parameters => parameters.Add(page => page.InventoryId, KitchenId));

        // The heading carries its own "what is this" mark after the name, so the name is looked for
        // inside it rather than compared to the whole of it.
        Assert.Contains(cut.FindAll("h2"), heading => heading.TextContent.Contains("Fridge"));
        Assert.Contains("Milk", cut.Markup);
        // And the group's own rows are still its own.
        Assert.Contains("Flour", cut.Markup);
    }

    [Fact]
    public void An_ordinary_shelfs_page_draws_no_headings_at_all()
    {
        _shelves[KitchenId] = [Batch("Flour", 1)];
        RegisterApiClients();

        var cut = RenderComponent<InventorySummary>(
            parameters => parameters.Add(page => page.InventoryId, KitchenId));

        Assert.Empty(cut.FindAll("h2"));
    }

    /// <summary>
    /// Ticking is the whole decision, so it is written where it is made rather than held for Save - the
    /// same rule the lists measured against a shelf follow. The whole membership goes up each time,
    /// which is what the server takes.
    /// </summary>
    [Fact]
    public void Ticking_a_shelf_in_the_editor_puts_it_in_the_group()
    {
        RegisterApiClients();
        var cut = RenderComponent<InventoryEditor>(
            parameters => parameters.Add(page => page.InventoryId, KitchenId));

        cut.FindAll(".inventory-gathers-card .check-row input").ToList()[0].Change(true);

        Assert.Equal(KitchenId, _gathered!.Value.InventoryId);
        Assert.Equal([FridgeId], _gathered!.Value.Members);
    }

    [Fact]
    public void And_unticking_one_takes_it_out_again()
    {
        _kitchenGathers = [FridgeId, PantryId];
        RegisterApiClients();
        var cut = RenderComponent<InventoryEditor>(
            parameters => parameters.Add(page => page.InventoryId, KitchenId));

        cut.FindAll(".inventory-gathers-card .check-row input").ToList()[0].Change(false);

        Assert.Equal([PantryId], _gathered!.Value.Members);
    }

    /// <summary>The shelf being edited is not among the ones it could gather - see Inventory.Gather.</summary>
    [Fact]
    public void A_shelf_is_never_offered_to_gather_itself()
    {
        RegisterApiClients();

        var cut = RenderComponent<InventoryEditor>(
            parameters => parameters.Add(page => page.InventoryId, KitchenId));

        var offered = cut.FindAll(".inventory-gathers-card .check-row").Select(row => row.TextContent).ToList();
        Assert.Equal(2, offered.Count);
        Assert.DoesNotContain(offered, row => row.Contains("Kitchen"));
    }

    private static InventoryItemDto Batch(string name, decimal quantity)
        => new(
            Guid.NewGuid(), name, "Food", "Dry", quantity, MinimumQuantity: null, Unit: "Piece",
            ExpiryDate: null, ExpiryNotificationChannel: "None", IsBelowMinimum: false,
            HasPendingRestockTask: false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private InventoryDto AnInventory(Guid id, string name)
        => new(
            id, name, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit",
            LockedByUserName: null, OriginalOwnerUserId: null,
            GathersInventoryIds: id == KitchenId ? _kitchenGathers : []);

    private void RegisterApiClients()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/gathers", StringComparison.Ordinal))
            {
                var asked = JsonSerializer.Deserialize<GatherInventoriesRequest>(
                    request.Content!.ReadAsStringAsync().GetAwaiter().GetResult(),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                _gathered = (InventoryIdIn(path), asked!.InventoryIds);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path == "/api/inventories")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new[]
                    {
                        AnInventory(KitchenId, "Kitchen"),
                        AnInventory(FridgeId, "Fridge"),
                        AnInventory(PantryId, "Pantry")
                    })
                };
            }

            if (path == "/api/tasks")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<TaskDto>()) };
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
                var inventoryId = InventoryIdIn(path);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(
                        _shelves.TryGetValue(inventoryId, out var shelf) ? shelf : [])
                };
            }

            if (path.EndsWith("/demand", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(Array.Empty<ShelfClaimDto>())
                };
            }

            if (path.EndsWith("/lock", StringComparison.Ordinal) || path.StartsWith("/api/share-links", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            // One inventory by its id, which is what both pages open on.
            var asked_id = InventoryIdIn(path);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(AnInventory(
                    asked_id,
                    asked_id == FridgeId ? "Fridge" : asked_id == PantryId ? "Pantry" : "Kitchen"))
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new InventoryApiClient(httpClient));
        Services.AddSingleton(new TasksApiClient(httpClient));
        Services.AddSingleton(new NotificationsApiClient(httpClient));
        Services.AddSingleton(new PublicShareApiClient(httpClient));
    }

    /// <summary>The inventory id out of "/api/inventories/{id}/…".</summary>
    private static Guid InventoryIdIn(string path)
        => path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => Guid.TryParse(segment, out var id) ? id : (Guid?)null)
            .OfType<Guid>()
            .FirstOrDefault();
}
