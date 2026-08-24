using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Inventory;
using Orbit.Core.Abstractions;

namespace Orbit.Web.Services;

/// <summary>
/// Thin wrapper around Orbit.Api's /api/inventory endpoints, keeping HTTP and JSON details out of the pages.
/// </summary>
public sealed class InventoryApiClient
{
    private readonly HttpClient _httpClient;

    public InventoryApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<InventoryItemDto>> GetInventoryItemsAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<InventoryItemDto>>("api/inventory", cancellationToken) ?? [];

    public async Task<InventoryItemDto?> GetInventoryItemByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/inventory/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InventoryItemDto>(cancellationToken: cancellationToken);
    }

    public async Task<Guid> CreateInventoryItemAsync(CreateInventoryItemRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/inventory", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    /// <summary>NotFound covers the item being missing or not owned by the caller - Inventory has no sharing/locking, so those are the only two outcomes this ever returns.</summary>
    public async Task<EditOutcome> UpdateInventoryItemAsync(Guid id, UpdateInventoryItemRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/inventory/{id}", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return EditOutcome.NotFound;
        }

        response.EnsureSuccessStatusCode();
        return EditOutcome.Success;
    }

    public async Task DeleteInventoryItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/inventory/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
