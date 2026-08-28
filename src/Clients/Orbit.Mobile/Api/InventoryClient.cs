using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Inventory;
using Orbit.Contracts.Sync;
using Orbit.Contracts.Sharing;

namespace Orbit.Mobile.Api;

/// <summary>
/// The warehouses half of the API.
///
/// Items have no endpoints of their own: they are created, changed and removed through the warehouse
/// save, exactly as task entries are through their task list. Reading them <b>is</b> separate, though -
/// the change feed describes a warehouse without saying what is in it - which is why
/// <see cref="GetItemsAsync"/> exists at all.
/// </summary>
public sealed class InventoryClient : ILockableItems
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

    /// <summary>
    /// Offers a copy to another account. The server records the offer; telling the recipient is this
    /// client's job, because the message that does it is end-to-end encrypted and only a client holds
    /// the key - see SharedItemSharing.
    /// </summary>
    public async Task<ShareResultDto?> ShareAsync(
        Guid warehouseId, Guid recipientUserId, string accessLevel, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/warehouses/{warehouseId}/shares", new { RecipientUserId = recipientUserId, AccessLevel = accessLevel },
            cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ShareResultDto>(cancellationToken)
            : null;
    }

    /// <inheritdoc cref="NotesClient.AcceptShareAsync"/>
    public async Task<bool> AcceptShareAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync($"api/warehouses/shares/{shareId}/accept", null, cancellationToken);
        return response.IsSuccessStatusCode;
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

    /// <summary>
    /// Whether this offer has already been taken up - by this phone, or by the same account somewhere
    /// else. Null when the server has never heard of the share, which a message older than the offer
    /// can produce. Orbit.Web asks the same question for the same reason: an "Accept" that has already
    /// been accepted is a button that can only disappoint.
    /// </summary>
    public async Task<bool?> IsShareAcceptedAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"api/warehouses/shares/{shareId}/status", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<bool>(cancellationToken)
            : null;
    }

    /// <summary>
    /// Claims this item while it is being edited, so a second editor is told rather than left to find
    /// out when their save is refused. Calling it again refreshes the claim - see EditLock.
    /// </summary>
    public Task<EditClaim> AcquireLockAsync(Guid serverId, CancellationToken cancellationToken = default)
        => EditLocking.AcquireAsync(_httpClient, $"api/warehouses/{serverId}/lock", cancellationToken);

    public Task ReleaseLockAsync(Guid serverId, CancellationToken cancellationToken = default)
        => EditLocking.ReleaseAsync(_httpClient, $"api/warehouses/{serverId}/lock", cancellationToken);
}
