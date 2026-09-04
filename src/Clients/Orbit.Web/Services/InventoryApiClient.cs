using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts;
using Orbit.Contracts.Inventories;
using Orbit.Contracts.Sharing;
using Orbit.Core.Abstractions;

namespace Orbit.Web.Services;

/// <summary>
/// Thin wrapper around Orbit.Api's /api/inventories endpoints, keeping HTTP and JSON details out of the
/// pages. Items are always addressed through their inventory, matching the API - see InventoryEndpoints.
/// </summary>
public sealed class InventoryApiClient
{
    private readonly HttpClient _httpClient;
    private readonly Translations? _translations;

    private readonly PrivateContentSealer? _privateContentSealer;

    /// <summary>Shown in place of a private inventory nobody can open any more - see PrivateContentSealer.OpenAsync.</summary>
    public const string UnreadableInventoryName = "Unreadable - encrypted with an older key";

    // The sealer and translations default to absent so existing call sites (including every test that
    // constructs this with just an HttpClient) keep compiling unchanged; only the DI-resolved instance
    // handles private inventories or speaks the reader's language.
    public InventoryApiClient(HttpClient httpClient, PrivateContentSealer? privateContentSealer = null, Translations? translations = null)
    {
        _httpClient = httpClient;
        _translations = translations;
        _privateContentSealer = privateContentSealer;
    }

    /// <summary>
    /// Hands back an ordinary inventory unchanged, and a private one with its real name put back. Its
    /// items come back through GetInventoryItemsAsync, which opens the same sealed payload - the server
    /// holds no item rows for a private inventory at all.
    /// </summary>
    private async Task<InventoryDto> OpenIfPrivateAsync(InventoryDto inventory, CancellationToken cancellationToken)
    {
        var content = await OpenContentAsync(inventory, cancellationToken);
        if (content is null)
        {
            return inventory.IsPrivate ? inventory with { Name = Translated(UnreadableInventoryName) } : inventory;
        }

        return inventory with { Name = content.Name };
    }

    private async Task<SealedInventory?> OpenContentAsync(InventoryDto inventory, CancellationToken cancellationToken)
        => inventory.IsPrivate && inventory.EncryptedContent is { } encryptedContent && _privateContentSealer is not null
            ? await _privateContentSealer.OpenAsync<SealedInventory>(encryptedContent, cancellationToken)
            : null;

    /// <summary>
    /// Seals a private inventory's name and items and empties the readable fields, so what leaves this
    /// browser matches what the server is allowed to hold. Left alone when it isn't private.
    /// </summary>
    private async Task<SaveInventoryRequest> SealIfPrivateAsync(SaveInventoryRequest request, CancellationToken cancellationToken)
    {
        if (!request.IsPrivate)
        {
            return request with { EncryptedContent = null };
        }

        if (_privateContentSealer is null)
        {
            throw new InvalidOperationException("This InventoryApiClient was built without a PrivateContentSealer, so it can't save a private inventory.");
        }

        var encryptedContent = await _privateContentSealer.SealAsync(
            new SealedInventory(request.Name, request.Items), cancellationToken);
        return request with { Name = string.Empty, Items = [], EncryptedContent = encryptedContent };
    }

    public async Task<IReadOnlyList<InventoryDto>> GetInventoriesAsync(CancellationToken cancellationToken = default)
    {
        var inventories = await _httpClient.GetFromJsonAsync<List<InventoryDto>>("api/inventories", cancellationToken) ?? [];

        var opened = new List<InventoryDto>(inventories.Count);
        foreach (var inventory in inventories)
        {
            opened.Add(await OpenIfPrivateAsync(inventory, cancellationToken));
        }

        return opened;
    }

    public async Task<InventoryDto?> GetInventoryByIdAsync(Guid inventoryId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/inventories/{inventoryId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var inventory = await response.Content.ReadFromJsonAsync<InventoryDto>(cancellationToken: cancellationToken);
        return inventory is null ? null : await OpenIfPrivateAsync(inventory, cancellationToken);
    }

    /// <summary>
    /// Creates the inventory, name and contents together, and says why if the server would not.
    ///
    /// It used to call EnsureSuccessStatusCode and hand back the id, which threw away the sentence the
    /// server writes into a refusal (see InvalidRequestExceptionHandler) and left the editor showing
    /// "Failed to save the inventory. Try again." - advice that could never work for a request the
    /// server had already explained. The save beside this one has read that body all along; this is the
    /// one that did not.
    /// </summary>
    public async Task<InventoryCreation> CreateInventoryAsync(
        SaveInventoryRequest request, CancellationToken cancellationToken = default)
    {
        request = await SealIfPrivateAsync(request, cancellationToken);
        var response = await _httpClient.PostAsJsonAsync("api/inventories", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var refusal = await response.Content.ReadFromJsonAsync<RefusalDto>(cancellationToken: cancellationToken);
            return InventoryCreation.Refused(refusal?.Message ?? Translated("Orbit refused that inventory."));
        }

        response.EnsureSuccessStatusCode();
        return InventoryCreation.Created(
            await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken));
    }

    /// <summary>Saves the inventory and its whole item list in one request - see SaveInventoryRequest.</summary>
    public async Task<EditOutcome> UpdateInventoryAsync(
        Guid inventoryId, SaveInventoryRequest request, CancellationToken cancellationToken = default)
    {
        request = await SealIfPrivateAsync(request, cancellationToken);
        var response = await _httpClient.PutAsJsonAsync($"api/inventories/{inventoryId}", request, cancellationToken);
        return await ToEditOutcomeAsync(response, cancellationToken);
    }

    /// <summary>Mirrors TasksApiClient.AcquireTaskListLockAsync - see its comment.</summary>
    public async Task<EditOutcome> AcquireInventoryLockAsync(Guid inventoryId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/inventories/{inventoryId}/lock", content: null, cancellationToken);
        return await ToEditOutcomeAsync(response, cancellationToken);
    }

    /// <summary>Mirrors TasksApiClient.ReleaseTaskListLockAsync - see its comment.</summary>
    public async Task ReleaseInventoryLockAsync(Guid inventoryId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/inventories/{inventoryId}/lock", cancellationToken);
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

    public async Task DeleteInventoryAsync(Guid inventoryId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/inventories/{inventoryId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Null when the inventory can't be shared by this caller - see ShareInventoryCommandHandler for the rules.</summary>
    public async Task<ShareResultDto?> ShareInventoryAsync(
        Guid inventoryId, Guid recipientUserId, string accessLevel, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/inventories/{inventoryId}/shares", new ShareInventoryRequest(recipientUserId, accessLevel), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ShareResultDto>(cancellationToken: cancellationToken);
    }

    public async Task<bool> AcceptInventoryShareAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/inventories/shares/{shareId}/accept", content: null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>Null when the share doesn't exist or wasn't offered to the caller; otherwise whether it's already accepted.</summary>
    public async Task<bool?> GetInventoryShareStatusAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/inventories/shares/{shareId}/status", cancellationToken);
        // Refused reads the same as absent from here: an account that has not unlocked sharing cannot
        // be told whether an offer was taken, and "no such offer" is the honest answer to give it. This
        // is asked in passing while a conversation is opened - see Chat - and left throwing, one 403
        // took the whole conversation down.
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken);
    }

    /// <summary>Null when the caller has no access to that inventory at all, as opposed to an empty list for one with no items.</summary>
    public async Task<IReadOnlyList<InventoryItemDto>?> GetInventoryItemsAsync(
        Guid inventoryId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/inventories/{inventoryId}/items", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<InventoryItemDto>>(cancellationToken: cancellationToken) ?? [];

        // A private inventory keeps no item rows on the server, so what comes back is empty by
        // definition and its real items live in the inventory's sealed payload.
        var inventory = await GetInventoryRawAsync(inventoryId, cancellationToken);
        if (inventory is null || !inventory.IsPrivate)
        {
            return items;
        }

        var content = await OpenContentAsync(inventory, cancellationToken);
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
    /// The inventory as the server sent it, sealed content and all - OpenIfPrivateAsync would have
    /// replaced the name already, and this needs the payload rather than the readable view of it.
    /// </summary>
    private async Task<InventoryDto?> GetInventoryRawAsync(Guid inventoryId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync($"api/inventories/{inventoryId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InventoryDto>(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Worked out here for a private inventory because the server can't: it derives this from item rows
    /// it no longer has. Same rule as InventoryItem.IsBelowMinimum - strictly below, so sitting exactly
    /// at the minimum is fine.
    /// </summary>
    private static bool IsBelowMinimum(InventoryItemRequest item)
        => item.MinimumQuantity is { } minimumQuantity && item.Quantity < minimumQuantity;

    /// <summary>
    /// The reader's language for text this client substitutes in - English when there is no
    /// Translations to ask, which is every test that builds this client by hand. Translated here
    /// rather than where it is rendered, because by then a stand-in title is indistinguishable from
    /// a real one the reader wrote.
    /// </summary>
    private string Translated(string english) => _translations?[english] ?? english;

    /// <summary>
    /// How this inventory's restock list is built and when it comes round. Null when the inventory is
    /// not one this reader may see.
    /// </summary>
    public async Task<RestockListSettingsDto?> GetRestockListSettingsAsync(
        Guid inventoryId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/inventories/{inventoryId}/restock-list/settings", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RestockListSettingsDto>(cancellationToken);
    }

    /// <summary>
    /// Saves the settings and rebuilds the list to match. What that moved is not read back: these are
    /// saved with the rest of the inventory now (see InventoryEditor.SaveAsync), and the page leaves for
    /// the list of inventories straight afterwards, so there is nowhere left to say "two added, one
    /// removed" to. Asking for the body anyway is what made a save fail on a response that carried none.
    /// </summary>
    public async Task SaveRestockListSettingsAsync(
        Guid inventoryId, RestockListSettingsDto settings, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"api/inventories/{inventoryId}/restock-list/settings", settings, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Rebuilds the list against the settings it already has - the Refresh button.</summary>
    public async Task<RestockRefreshResultDto> RefreshRestockListAsync(
        Guid inventoryId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            $"api/inventories/{inventoryId}/restock-list/refresh", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RestockRefreshResultDto>(cancellationToken)
            ?? new RestockRefreshResultDto(0, 0);
    }
}
