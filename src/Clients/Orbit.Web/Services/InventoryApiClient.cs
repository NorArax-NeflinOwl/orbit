using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Inventory;
using Orbit.Contracts.Sharing;
using Orbit.Core.Abstractions;

namespace Orbit.Web.Services;

/// <summary>
/// Thin wrapper around Orbit.Api's /api/warehouses endpoints, keeping HTTP and JSON details out of the
/// pages. Items are always addressed through their warehouse, matching the API - see InventoryEndpoints.
/// </summary>
public sealed class InventoryApiClient
{
    private readonly HttpClient _httpClient;

    public InventoryApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<WarehouseDto>>("api/warehouses", cancellationToken) ?? [];

    public async Task<WarehouseDto?> GetWarehouseByIdAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/warehouses/{warehouseId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WarehouseDto>(cancellationToken: cancellationToken);
    }

    public async Task<Guid> CreateWarehouseAsync(SaveWarehouseRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/warehouses", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task<EditOutcome> UpdateWarehouseAsync(
        Guid warehouseId, SaveWarehouseRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/warehouses/{warehouseId}", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return EditOutcome.NotFound;
        }

        response.EnsureSuccessStatusCode();
        return EditOutcome.Success;
    }

    public async Task DeleteWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/warehouses/{warehouseId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Null when the warehouse can't be shared by this caller - see ShareWarehouseCommandHandler for the rules.</summary>
    public async Task<ShareResultDto?> ShareWarehouseAsync(
        Guid warehouseId, Guid recipientUserId, string accessLevel, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/warehouses/{warehouseId}/shares", new ShareWarehouseRequest(recipientUserId, accessLevel), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ShareResultDto>(cancellationToken: cancellationToken);
    }

    public async Task<bool> AcceptWarehouseShareAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/warehouses/shares/{shareId}/accept", content: null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>Null when the share doesn't exist or wasn't offered to the caller; otherwise whether it's already accepted.</summary>
    public async Task<bool?> GetWarehouseShareStatusAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/warehouses/shares/{shareId}/status", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken);
    }

    /// <summary>Null when the caller has no access to that warehouse at all, as opposed to an empty list for one with no items.</summary>
    public async Task<IReadOnlyList<InventoryItemDto>?> GetInventoryItemsAsync(
        Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/warehouses/{warehouseId}/items", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<InventoryItemDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<InventoryItemDto?> GetInventoryItemByIdAsync(
        Guid warehouseId, Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/warehouses/{warehouseId}/items/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InventoryItemDto>(cancellationToken: cancellationToken);
    }

    /// <summary>Null when the warehouse isn't writable by the caller - missing, not shared with them, or shared read-only.</summary>
    public async Task<Guid?> CreateInventoryItemAsync(
        Guid warehouseId, CreateInventoryItemRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/warehouses/{warehouseId}/items", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    /// <summary>NotFound covers the item being missing and the warehouse not being writable by the caller - items have no lock, so those are the only two outcomes.</summary>
    public async Task<EditOutcome> UpdateInventoryItemAsync(
        Guid warehouseId, Guid id, UpdateInventoryItemRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/warehouses/{warehouseId}/items/{id}", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return EditOutcome.NotFound;
        }

        response.EnsureSuccessStatusCode();
        return EditOutcome.Success;
    }

    public async Task DeleteInventoryItemAsync(Guid warehouseId, Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/warehouses/{warehouseId}/items/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
