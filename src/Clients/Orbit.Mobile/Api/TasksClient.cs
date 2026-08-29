using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Sync;
using Orbit.Contracts.Tasks;
using Orbit.Contracts.Sharing;

namespace Orbit.Mobile.Api;

/// <summary>
/// The task lists half of the API. Only the synchroniser calls this - screens read the local database,
/// exactly as they do for notes.
/// </summary>
public sealed class TasksClient : ILockableItems
{
    private readonly HttpClient _httpClient;

    public TasksClient(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>
    /// What changed since <paramref name="cursor"/>, and what was deleted. A null cursor asks for
    /// everything, which is what a device that has never synced needs.
    /// </summary>
    public async Task<ChangeFeedDto<TaskDto>> GetChangesAsync(string? cursor, CancellationToken cancellationToken = default)
    {
        var since = cursor ?? DateTimeOffset.MinValue.UtcDateTime.ToString("O");
        return await _httpClient.GetFromJsonAsync<ChangeFeedDto<TaskDto>>(
            $"api/tasks/changes?since={Uri.EscapeDataString(since)}", cancellationToken)
            ?? new ChangeFeedDto<TaskDto>([], [], since);
    }

    public async Task<Guid> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/tasks", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken);
    }

    public async Task<WriteOutcome> UpdateAsync(
        Guid taskListId, UpdateTaskRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/tasks/{taskListId}", request, cancellationToken);
        return ReadOutcome(response);
    }

    /// <summary>
    /// Offers a copy to another account. The server records the offer; telling the recipient is this
    /// client's job, because the message that does it is end-to-end encrypted and only a client holds
    /// the key - see SharedItemSharing.
    /// </summary>
    public async Task<ShareResultDto?> ShareAsync(
        Guid taskListId, Guid recipientUserId, string accessLevel, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/tasks/{taskListId}/shares", new { RecipientUserId = recipientUserId, AccessLevel = accessLevel },
            cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ShareResultDto>(cancellationToken)
            : null;
    }

    /// <summary>
    /// Turns an offer into a copy in this account's own task lists. The offer itself arrived as a chat
    /// message; this is the half the server acts on - see SharedItemInvitation.
    /// </summary>
    public async Task<bool> AcceptShareAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync($"api/tasks/shares/{shareId}/accept", null, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Its own endpoint rather than part of an update, because pinning is not a change to the list: it
    /// leaves UpdatedAtUtc alone, and only the owner may do it.
    /// </summary>
    public async Task<WriteOutcome> SetPinnedAsync(
        Guid taskListId, bool isPinned, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"api/tasks/{taskListId}/pinned", new SetTaskListPinnedRequest(isPinned), cancellationToken);

        return ReadOutcome(response);
    }

    public async Task<WriteOutcome> DeleteAsync(Guid taskListId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/tasks/{taskListId}", cancellationToken);
        // A list already gone is the outcome the caller wanted, not a failure to retry.
        return response.StatusCode is HttpStatusCode.NotFound ? WriteOutcome.Applied : ReadOutcome(response);
    }

    /// <summary>
    /// Anything not named here - a server error, a gateway timeout - throws, so the queued change stays
    /// queued and is tried again. Only a refusal the server will repeat is worth giving up on.
    /// </summary>
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
    /// Builds the shelf this list's work needs - one entry per distinct thing it calls for, each
    /// starting at nothing - and points the list at it. Null when there was nothing to build.
    /// </summary>
    public async Task<Guid?> GenerateInventoryAsync(Guid taskListId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync($"api/tasks/{taskListId}/inventory", content: null, cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<Guid>(cancellationToken)
            : null;
    }

    /// <summary>Points a task list at the warehouse its work is measured against, or at none.</summary>
    public async Task<bool> LinkWarehouseAsync(
        Guid taskListId, Guid? warehouseId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            $"api/tasks/{taskListId}/warehouse", new LinkTaskListToWarehouseRequest(warehouseId), cancellationToken);

        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// What this list's work costs against that warehouse, or null when no warehouse has been chosen -
    /// there is no question to answer then, which is not the same as an answer of "nothing".
    /// </summary>
    public async Task<TaskListStockCheckDto?> GetStockCheckAsync(
        Guid taskListId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"api/tasks/{taskListId}/stock-check", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TaskListStockCheckDto>(cancellationToken)
            : null;
    }

    /// <summary>
    /// Reads the warehouse again and brings the list back into step with it both ways: crossing off what
    /// the shelf turns out to cover, and writing on what the shelf holds that nothing here asked for.
    ///
    /// Nothing moved when the server refuses, which is what the zeroes say - the caller reports what the
    /// reconciliation did, and "it did nothing" is a true answer to give for a call that did not land.
    /// </summary>
    public async Task<StockReconciliationResultDto> ReconcileWithStockAsync(
        Guid taskListId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(
            $"api/tasks/{taskListId}/stock-check/reconciliation", content: null, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new StockReconciliationResultDto(0, 0);
        }

        return await response.Content.ReadFromJsonAsync<StockReconciliationResultDto>(cancellationToken)
            ?? new StockReconciliationResultDto(0, 0);
    }

    /// <summary>Puts what is short onto the warehouse's restock list. Returns how many entries were added.</summary>
    public async Task<int> RaiseStockShortfallsAsync(Guid taskListId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(
            $"api/tasks/{taskListId}/stock-check/shortfalls", content: null, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return 0;
        }

        var result = await response.Content.ReadFromJsonAsync<RaiseStockShortfallsResultDto>(cancellationToken);
        return result?.AddedCount ?? 0;
    }

    /// <summary>
    /// Moves one entry out of this list and into another. Done against the server rather than queued:
    /// it needs both lists' real ids, and there is no sensible local half of "it is now over there".
    /// </summary>
    public async Task<WriteOutcome> MoveItemAsync(
        Guid sourceTaskListId, Guid itemId, Guid targetTaskListId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/tasks/{sourceTaskListId}/items/{itemId}/move", new MoveTaskItemRequest(targetTaskListId),
            cancellationToken);

        return ReadOutcome(response);
    }

    /// <summary>
    /// Whether this offer has already been taken up - by this phone, or by the same account somewhere
    /// else. Null when the server has never heard of the share, which a message older than the offer
    /// can produce. Orbit.Web asks the same question for the same reason: an "Accept" that has already
    /// been accepted is a button that can only disappoint.
    /// </summary>
    public async Task<bool?> IsShareAcceptedAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"api/tasks/shares/{shareId}/status", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<bool>(cancellationToken)
            : null;
    }

    /// <summary>
    /// Claims this item while it is being edited, so a second editor is told rather than left to find
    /// out when their save is refused. Calling it again refreshes the claim - see EditLock.
    /// </summary>
    public Task<EditClaim> AcquireLockAsync(Guid serverId, CancellationToken cancellationToken = default)
        => EditLocking.AcquireAsync(_httpClient, $"api/tasks/{serverId}/lock", cancellationToken);

    public Task ReleaseLockAsync(Guid serverId, CancellationToken cancellationToken = default)
        => EditLocking.ReleaseAsync(_httpClient, $"api/tasks/{serverId}/lock", cancellationToken);
}
