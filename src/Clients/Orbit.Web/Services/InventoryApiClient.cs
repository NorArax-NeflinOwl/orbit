using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts;
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
    private readonly Translations? _translations;

    private readonly PrivateContentSealer? _privateContentSealer;

    /// <summary>Shown in place of a private warehouse nobody can open any more - see PrivateContentSealer.OpenAsync.</summary>
    public const string UnreadableWarehouseName = "Unreadable - encrypted with an older key";

    // The sealer and translations default to absent so existing call sites (including every test that
    // constructs this with just an HttpClient) keep compiling unchanged; only the DI-resolved instance
    // handles private warehouses or speaks the reader's language.
    public InventoryApiClient(HttpClient httpClient, PrivateContentSealer? privateContentSealer = null, Translations? translations = null)
    {
        _httpClient = httpClient;
        _translations = translations;
        _privateContentSealer = privateContentSealer;
    }

    /// <summary>
    /// Hands back an ordinary warehouse unchanged, and a private one with its real name put back. Its
    /// items come back through GetInventoryItemsAsync, which opens the same sealed payload - the server
    /// holds no item rows for a private warehouse at all.
    /// </summary>
    private async Task<WarehouseDto> OpenIfPrivateAsync(WarehouseDto warehouse, CancellationToken cancellationToken)
    {
        var content = await OpenContentAsync(warehouse, cancellationToken);
        if (content is null)
        {
            return warehouse.IsPrivate ? warehouse with { Name = Translated(UnreadableWarehouseName) } : warehouse;
        }

        return warehouse with { Name = content.Name };
    }

    private async Task<SealedWarehouse?> OpenContentAsync(WarehouseDto warehouse, CancellationToken cancellationToken)
        => warehouse.IsPrivate && warehouse.EncryptedContent is { } encryptedContent && _privateContentSealer is not null
            ? await _privateContentSealer.OpenAsync<SealedWarehouse>(encryptedContent, cancellationToken)
            : null;

    /// <summary>
    /// Seals a private warehouse's name and items and empties the readable fields, so what leaves this
    /// browser matches what the server is allowed to hold. Left alone when it isn't private.
    /// </summary>
    private async Task<SaveWarehouseRequest> SealIfPrivateAsync(SaveWarehouseRequest request, CancellationToken cancellationToken)
    {
        if (!request.IsPrivate)
        {
            return request with { EncryptedContent = null };
        }

        if (_privateContentSealer is null)
        {
            throw new InvalidOperationException("This InventoryApiClient was built without a PrivateContentSealer, so it can't save a private warehouse.");
        }

        var encryptedContent = await _privateContentSealer.SealAsync(
            new SealedWarehouse(request.Name, request.Items), cancellationToken);
        return request with { Name = string.Empty, Items = [], EncryptedContent = encryptedContent };
    }

    /// <summary>Everything a private warehouse hides from the server, as one sealed payload.</summary>
    private sealed record SealedWarehouse(string Name, IReadOnlyList<WarehouseItemDto> Items);

    public async Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(CancellationToken cancellationToken = default)
    {
        var warehouses = await _httpClient.GetFromJsonAsync<List<WarehouseDto>>("api/warehouses", cancellationToken) ?? [];

        var opened = new List<WarehouseDto>(warehouses.Count);
        foreach (var warehouse in warehouses)
        {
            opened.Add(await OpenIfPrivateAsync(warehouse, cancellationToken));
        }

        return opened;
    }

    public async Task<WarehouseDto?> GetWarehouseByIdAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/warehouses/{warehouseId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var warehouse = await response.Content.ReadFromJsonAsync<WarehouseDto>(cancellationToken: cancellationToken);
        return warehouse is null ? null : await OpenIfPrivateAsync(warehouse, cancellationToken);
    }

    public async Task<Guid> CreateWarehouseAsync(SaveWarehouseRequest request, CancellationToken cancellationToken = default)
    {
        request = await SealIfPrivateAsync(request, cancellationToken);
        var response = await _httpClient.PostAsJsonAsync("api/warehouses", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    /// <summary>Saves the warehouse and its whole item list in one request - see SaveWarehouseRequest.</summary>
    public async Task<EditOutcome> UpdateWarehouseAsync(
        Guid warehouseId, SaveWarehouseRequest request, CancellationToken cancellationToken = default)
    {
        request = await SealIfPrivateAsync(request, cancellationToken);
        var response = await _httpClient.PutAsJsonAsync($"api/warehouses/{warehouseId}", request, cancellationToken);
        return await ToEditOutcomeAsync(response, cancellationToken);
    }

    /// <summary>Mirrors TasksApiClient.AcquireTaskListLockAsync - see its comment.</summary>
    public async Task<EditOutcome> AcquireWarehouseLockAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/warehouses/{warehouseId}/lock", content: null, cancellationToken);
        return await ToEditOutcomeAsync(response, cancellationToken);
    }

    /// <summary>Mirrors TasksApiClient.ReleaseTaskListLockAsync - see its comment.</summary>
    public async Task ReleaseWarehouseLockAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/warehouses/{warehouseId}/lock", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<EditOutcome> ToEditOutcomeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return EditOutcome.NotFound;
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var conflict = await response.Content.ReadFromJsonAsync<LockConflictDto>(cancellationToken: cancellationToken);
            return EditOutcome.LockedBy(conflict?.LockedByUserName ?? Translated("another user"));
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            var refusal = await response.Content.ReadFromJsonAsync<RefusalDto>(cancellationToken: cancellationToken);
            return EditOutcome.RefusedBecause(refusal?.Message ?? Translated("This was shared with you to read, not to change."));
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // The server explains a refusal in the body (see InvalidRequestExceptionHandler); throwing
            // that away left the reader with "something went wrong" and no way to find out what.
            var refusal = await response.Content.ReadFromJsonAsync<RefusalDto>(cancellationToken: cancellationToken);
            return EditOutcome.RefusedBecause(refusal?.Message ?? Translated("Orbit refused that change."));
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
        var items = await response.Content.ReadFromJsonAsync<List<InventoryItemDto>>(cancellationToken: cancellationToken) ?? [];

        // A private warehouse keeps no item rows on the server, so what comes back is empty by
        // definition and its real items live in the warehouse's sealed payload.
        var warehouse = await GetWarehouseRawAsync(warehouseId, cancellationToken);
        if (warehouse is null || !warehouse.IsPrivate)
        {
            return items;
        }

        var content = await OpenContentAsync(warehouse, cancellationToken);
        return content is null
            ? []
            : content.Items
                .Select(item => new InventoryItemDto(
                    item.Id ?? Guid.Empty, item.Name, item.ProductType, item.Category, item.Quantity,
                    item.MinimumQuantity, item.Unit, item.ExpiryDate, item.ExpiryNotificationChannel,
                    IsBelowMinimum(item), HasPendingRestockTask: false,
                    // The server keeps no rows for these, so it has no timestamps to report either.
                    CreatedAtUtc: default, UpdatedAtUtc: default))
                .ToList();
    }

    /// <summary>
    /// The warehouse as the server sent it, sealed content and all - OpenIfPrivateAsync would have
    /// replaced the name already, and this needs the payload rather than the readable view of it.
    /// </summary>
    private async Task<WarehouseDto?> GetWarehouseRawAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync($"api/warehouses/{warehouseId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WarehouseDto>(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Worked out here for a private warehouse because the server can't: it derives this from item rows
    /// it no longer has. Same rule as InventoryItem.IsBelowMinimum - strictly below, so sitting exactly
    /// at the minimum is fine.
    /// </summary>
    private static bool IsBelowMinimum(WarehouseItemDto item)
        => item.MinimumQuantity is { } minimumQuantity && item.Quantity < minimumQuantity;

    /// <summary>
    /// The reader's language for text this client substitutes in - English when there is no
    /// Translations to ask, which is every test that builds this client by hand. Translated here
    /// rather than where it is rendered, because by then a stand-in title is indistinguishable from
    /// a real one the reader wrote.
    /// </summary>
    private string Translated(string english) => _translations?[english] ?? english;

    /// <summary>
    /// How this warehouse's restock list is built and when it comes round. Null when the warehouse is
    /// not one this reader may see.
    /// </summary>
    public async Task<RestockListSettingsDto?> GetRestockListSettingsAsync(
        Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/warehouses/{warehouseId}/restock-list/settings", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RestockListSettingsDto>(cancellationToken);
    }

    /// <summary>Saves the settings and rebuilds the list to match, answering what that moved.</summary>
    public async Task<RestockRefreshResultDto> SaveRestockListSettingsAsync(
        Guid warehouseId, RestockListSettingsDto settings, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"api/warehouses/{warehouseId}/restock-list/settings", settings, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RestockRefreshResultDto>(cancellationToken)
            ?? new RestockRefreshResultDto(0, 0);
    }

    /// <summary>Rebuilds the list against the settings it already has - the Refresh button.</summary>
    public async Task<RestockRefreshResultDto> RefreshRestockListAsync(
        Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            $"api/warehouses/{warehouseId}/restock-list/refresh", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RestockRefreshResultDto>(cancellationToken)
            ?? new RestockRefreshResultDto(0, 0);
    }
}
