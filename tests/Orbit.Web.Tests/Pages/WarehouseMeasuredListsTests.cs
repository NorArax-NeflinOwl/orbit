using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Inventory;
using Orbit.Contracts.Notifications;
using Orbit.Contracts.Tasks;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// Which lists a warehouse is measured against, said from the warehouse's end. The tie could only be
/// made from a task list before, which is the wrong way round for somebody standing in the pantry
/// thinking about which jobs it serves - and several lists may share one shelf now, so it is a list of
/// boxes rather than a single choice.
/// </summary>
public sealed class WarehouseMeasuredListsTests : OrbitTestContext
{
    private static readonly Guid WarehouseId = Guid.NewGuid();
    private static readonly Guid ShedId = Guid.NewGuid();
    private static readonly Guid BakingId = Guid.NewGuid();
    private static readonly Guid BreadId = Guid.NewGuid();

    /// <summary>Every warehouse a list was pointed at, in the order the page asked for it.</summary>
    private readonly List<(Guid TaskListId, Guid? WarehouseId)> _linked = [];

    private IReadOnlyList<TaskDto> _taskLists = [];

    public WarehouseMeasuredListsTests() => Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

    [Fact]
    public void Every_list_is_offered_and_the_ones_measured_here_are_ticked()
    {
        _taskLists = [AList(BakingId, "Baking", WarehouseId), AList(BreadId, "Bread", warehouseId: null)];
        RegisterApiClients();

        var cut = RenderComponent<WarehouseEditor>(parameters => parameters.Add(page => page.WarehouseId, WarehouseId));

        var rows = cut.FindAll(".check-row").ToList();
        Assert.Equal(2, rows.Count);
        Assert.Contains("Baking", rows[0].TextContent);
        Assert.True(rows[0].QuerySelector("input")!.HasAttribute("checked"));
        Assert.False(rows[1].QuerySelector("input")!.HasAttribute("checked"));
    }

    /// <summary>Ticking is the whole decision, so it is written where it is made rather than held for Save.</summary>
    [Fact]
    public void Ticking_a_list_measures_it_against_this_warehouse()
    {
        _taskLists = [AList(BreadId, "Bread", warehouseId: null)];
        RegisterApiClients();
        var cut = RenderComponent<WarehouseEditor>(parameters => parameters.Add(page => page.WarehouseId, WarehouseId));

        cut.Find(".check-row input").Change(true);

        Assert.Equal((BreadId, WarehouseId), Assert.Single(_linked));
    }

    [Fact]
    public void Unticking_a_list_lets_the_warehouse_go()
    {
        _taskLists = [AList(BakingId, "Baking", WarehouseId)];
        RegisterApiClients();
        var cut = RenderComponent<WarehouseEditor>(parameters => parameters.Add(page => page.WarehouseId, WarehouseId));

        cut.Find(".check-row input").Change(false);

        Assert.Equal((BakingId, (Guid?)null), Assert.Single(_linked));
    }

    /// <summary>
    /// A list already measured against another store would be moved by a tick here, so where it would
    /// leave is named on its row rather than discovered afterwards.
    /// </summary>
    [Fact]
    public void A_list_measured_elsewhere_says_where()
    {
        _taskLists = [AList(BakingId, "Baking", ShedId)];
        RegisterApiClients();

        var cut = RenderComponent<WarehouseEditor>(parameters => parameters.Add(page => page.WarehouseId, WarehouseId));

        Assert.Contains("Shed", cut.Find(".check-row .row-meta").TextContent);
    }

    private static TaskDto AList(Guid id, string title, Guid? warehouseId)
        => new(
            id, title, [], IsCompleted: false, IsGroup: false, IsPrivate: false, EncryptedContent: null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null,
            LinkedWarehouseId: warehouseId);

    private void RegisterApiClients()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/warehouse", StringComparison.Ordinal))
            {
                var asked = JsonSerializer.Deserialize<LinkTaskListToWarehouseRequest>(
                    request.Content!.ReadAsStringAsync().GetAwaiter().GetResult(),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                _linked.Add((WarehouseIdIn(path), asked!.WarehouseId));
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path == "/api/tasks")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(_taskLists) };
            }

            if (path == "/api/warehouses")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new[] { AWarehouse(WarehouseId, "Pantry"), AWarehouse(ShedId, "Shed") })
                };
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
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<InventoryItemDto>()) };
            }

            if (path.EndsWith("/lock", StringComparison.Ordinal) || path.StartsWith("/api/share-links", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(AWarehouse(WarehouseId, "Pantry")) };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new InventoryApiClient(httpClient));
        Services.AddSingleton(new TasksApiClient(httpClient));
        Services.AddSingleton(new NotificationsApiClient(httpClient));
        // The page carries a share link too - see ShareLinkButton, which asks on render.
        Services.AddSingleton(new PublicShareApiClient(httpClient));
    }

    private static WarehouseDto AWarehouse(Guid id, string name)
        => new(
            id, name, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit",
            LockedByUserName: null, OriginalOwnerUserId: null);

    /// <summary>The list a "/api/tasks/{id}/warehouse" path names.</summary>
    private static Guid WarehouseIdIn(string path)
        => Guid.TryParse(path.Split('/').SkipLast(1).Last(), out var id) ? id : Guid.Empty;
}
