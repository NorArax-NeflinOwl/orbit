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
    /// Claims this item while it is being edited, so a second editor is told rather than left to find
    /// out when their save is refused. Calling it again refreshes the claim - see EditLock.
    /// </summary>
    public Task<EditClaim> AcquireLockAsync(Guid serverId, CancellationToken cancellationToken = default)
        => EditLocking.AcquireAsync(_httpClient, $"api/tasks/{serverId}/lock", cancellationToken);

    public Task ReleaseLockAsync(Guid serverId, CancellationToken cancellationToken = default)
        => EditLocking.ReleaseAsync(_httpClient, $"api/tasks/{serverId}/lock", cancellationToken);
}
