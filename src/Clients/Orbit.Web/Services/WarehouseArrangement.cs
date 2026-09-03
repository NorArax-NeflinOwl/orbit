using System.Text.Json;
using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// The order this reader put their warehouses in.
///
/// Kept on the device rather than on the account, like <see cref="TaskListArrangement"/>: it describes
/// how one person reads one page and says nothing about the warehouses themselves - somebody a shelf is
/// shared with has their own idea of which one matters most, and neither answer is the right one to
/// impose on the other.
/// </summary>
public sealed class WarehouseArrangement
{
    private const string OrderKey = "orbit-warehouse-order";

    private readonly IJSRuntime _jsRuntime;

    public WarehouseArrangement(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// The ids the reader has arranged, first to last. A warehouse that is not on it - one created or
    /// shared since the last move - is not in the wrong place, it simply has not been placed, and the
    /// page puts those after the ones that have.
    /// </summary>
    public IReadOnlyList<Guid> Order { get; private set; } = [];

    public async Task InitializeAsync() => Order = Read(await ReadAsync(OrderKey));

    public Task SetOrderAsync(IReadOnlyList<Guid> orderedWarehouseIds)
    {
        Order = orderedWarehouseIds;
        return WriteAsync(OrderKey, JsonSerializer.Serialize(orderedWarehouseIds));
    }

    /// <summary>
    /// The warehouses in the arranged order, with anything unplaced after them in the order it arrived.
    /// </summary>
    public IEnumerable<T> Arrange<T>(IEnumerable<T> warehouses, Func<T, Guid> idOf)
    {
        var placed = Order
            .Select((id, index) => (id, index))
            .ToDictionary(entry => entry.id, entry => entry.index);

        return warehouses.OrderBy(warehouse => placed.TryGetValue(idOf(warehouse), out var index) ? index : int.MaxValue);
    }

    /// <summary>An unreadable or absent value means nothing has been arranged yet, which is a fine answer.</summary>
    private static IReadOnlyList<Guid> Read(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(stored) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Mirrors DevicePreferences: a browser with storage blocked outright (private windows, embedded
    /// webviews) throws here, and the right answer then is the default.
    /// </summary>
    private async Task<string?> ReadAsync(string key)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch (JSException)
        {
            return null;
        }
    }

    private async Task WriteAsync(string key, string value)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
        }
        catch (JSException)
        {
            // It still applies for this session - it just won't be remembered for the next one.
        }
    }
}
