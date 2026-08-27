using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Orbit.Contracts.Inventory;
using Orbit.Contracts.Sync;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// Orbit's warehouse endpoints, in memory - including the part that makes this entity type different:
/// the change feed describes a warehouse without saying what is in it, and items are served separately.
/// </summary>
internal sealed class FakeInventoryServer : HttpMessageHandler
{
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<Guid, WarehouseDto> _warehouses = [];
    private readonly Dictionary<Guid, List<InventoryItemDto>> _items = [];
    private readonly List<(Guid Id, DateTimeOffset DeletedAtUtc)> _tombstones = [];

    public FakeInventoryServer(TimeProvider timeProvider) => _timeProvider = timeProvider;

    public List<string> ReceivedRequests { get; } = [];

    public bool IsUnreachable { get; set; }

    public IReadOnlyCollection<WarehouseDto> Warehouses => _warehouses.Values;

    public IReadOnlyList<InventoryItemDto> ItemsIn(Guid warehouseId)
        => _items.TryGetValue(warehouseId, out var items) ? items : [];

    public WarehouseDto AddWarehouse(string name, bool isSharedWithOthers = false)
    {
        var now = _timeProvider.GetUtcNow();
        var warehouse = new WarehouseDto(
            Guid.NewGuid(), name, now, now, false, null, "CanEdit", null, null, false, null, isSharedWithOthers);

        _warehouses[warehouse.Id] = warehouse;
        _items[warehouse.Id] = [];
        return warehouse;
    }

    public void AddItem(Guid warehouseId, string name, decimal quantity)
    {
        var now = _timeProvider.GetUtcNow();
        _items[warehouseId].Add(new InventoryItemDto(
            Guid.NewGuid(), name, "Piece", "General", quantity, null, null, "None", false, false, now, now));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        ReceivedRequests.Add($"{request.Method} {path}");

        if (IsUnreachable)
        {
            throw new HttpRequestException("No such host is known.");
        }

        // Nobody else is ever in it here; EditLockTests covers the answer where somebody is.
        if (path.EndsWith("/lock", StringComparison.Ordinal))
        {
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (path.EndsWith("/changes", StringComparison.Ordinal))
        {
            var since = DateTimeOffset.Parse(HttpUtility.ParseQueryString(request.RequestUri.Query)["since"]!);
            return Json(new ChangeFeedDto<WarehouseDto>(
                _warehouses.Values.Where(item => item.UpdatedAtUtc >= since).ToList(),
                _tombstones.Where(entry => entry.DeletedAtUtc >= since).Select(entry => entry.Id).ToList(),
                _timeProvider.GetUtcNow().UtcDateTime.ToString("O")));
        }

        if (path.EndsWith("/items", StringComparison.Ordinal))
        {
            var warehouseId = Guid.Parse(path.Split('/')[^2]);
            return Json(ItemsIn(warehouseId).ToList());
        }

        return request.Method.Method switch
        {
            "POST" => await CreateAsync(request, cancellationToken),
            "PUT" => await SaveAsync(request, path, cancellationToken),
            "DELETE" => Delete(path),
            _ => Json(_warehouses.Values.ToList())
        };
    }

    private async Task<HttpResponseMessage> CreateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = await ReadAsync<SaveWarehouseRequest>(request, cancellationToken);
        return Json(AddWarehouse(body!.Name).Id, HttpStatusCode.Created);
    }

    private async Task<HttpResponseMessage> SaveAsync(HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        var id = Guid.Parse(path.Split('/')[^1]);
        if (!_warehouses.TryGetValue(id, out var existing))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var body = await ReadAsync<SaveWarehouseRequest>(request, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        _warehouses[id] = existing with { Name = body!.Name, UpdatedAtUtc = now };

        // A save carries the whole intended list: anything missing from it is gone, and an item that
        // came back with its id keeps that id.
        _items[id] = body.Items.Select(item => new InventoryItemDto(
            item.Id ?? Guid.NewGuid(), item.Name, item.ProductType, item.Category, item.Quantity,
            item.MinimumQuantity, item.ExpiryDate, item.ExpiryNotificationChannel, false, false, now, now)).ToList();

        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private HttpResponseMessage Delete(string path)
    {
        var id = Guid.Parse(path.Split('/')[^1]);
        if (!_warehouses.Remove(id))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        _items.Remove(id);
        _tombstones.Add((id, _timeProvider.GetUtcNow()));
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private static async Task<T?> ReadAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
        => JsonSerializer.Deserialize<T>(
            await request.Content!.ReadAsStringAsync(cancellationToken),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static HttpResponseMessage Json<T>(T payload, HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode) { Content = JsonContent.Create(payload) };

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.example/") };
}
