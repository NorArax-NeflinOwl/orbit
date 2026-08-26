using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Inventory;
using Orbit.Contracts.Sync;

namespace Orbit.Mobile.Api;

/// <summary>
/// The warehouses half of the API.
///
/// Items have no endpoints of their own: they are created, changed and removed through the warehouse
/// save, exactly as task entries are through their task list. Reading them <b>is</b> separate, though -
/// the change feed describes a warehouse without saying what is in it - which is why
/// <see cref="GetItemsAsync"/> exists at all.
/// </summary>
public sealed class InventoryClient
{
    private readonly HttpClient _httpClient;

    public InventoryClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<ChangeFeedDto<WarehouseDto>> GetChangesAsync(
        string? cursor, CancellationToken cancellationToken = default)
    {
        var since = cursor ?? DateTimeOffset.MinValue.UtcDateTime.ToString("O");
        return await _httpClient.GetFromJsonAsync<ChangeFeedDto<WarehouseDto>>(
            $"api/warehouses/changes?since={Uri.EscapeDataString(since)}", cancellationToken)
            ?? new ChangeFeedDto<WarehouseDto>([], [], since);
    }

    /// <summary>What one warehouse holds. Empty when it is gone, which a pull can race with.</summary>
    public async Task<IReadOnlyList<InventoryItemDto>> GetItemsAsync(
        Guid warehouseId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"api/warehouses/{warehouseId}/items", cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<InventoryItemDto>>(cancellationToken) ?? [];
    }

    public async Task<Guid> CreateAsync(SaveWarehouseRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/warehouses", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken);
    }

    public async Task<WriteOutcome> UpdateAsync(
        Guid warehouseId, SaveWarehouseRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/warehouses/{warehouseId}", request, cancellationToken);
        return ReadOutcome(response);
    }

    public async Task<WriteOutcome> DeleteAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/warehouses/{warehouseId}", cancellationToken);
        return response.StatusCode is HttpStatusCode.NotFound ? WriteOutcome.Applied : ReadOutcome(response);
    }

    /// <summary>Anything not named here throws, so the queued change stays queued and is tried again.</summary>
    private static WriteOutcome ReadOutcome(HttpResponseMessage response)
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.Conflict:
                return WriteOutcome.Refused;
            case HttpStatusCode.NotFound:
                return WriteOutcome.Gone;
            default:
                response.EnsureSuccessStatusCode();
                return WriteOutcome.Applied;
        }
    }
}
