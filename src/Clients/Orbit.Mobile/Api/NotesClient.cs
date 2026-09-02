using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Sync;
using Orbit.Contracts.Sharing;

namespace Orbit.Mobile.Api;

/// <summary>
/// The notes half of the API. Only the synchroniser calls this - screens read the local database, and
/// the sync layer is what keeps the two in step (see info/orbit-maui-plan.md §5).
/// </summary>
public sealed class NotesClient : ILockableItems
{
    private readonly HttpClient _httpClient;

    public NotesClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<IReadOnlyList<NoteDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<NoteDto>>("api/notes", cancellationToken) ?? [];

    /// <summary>
    /// What changed since <paramref name="cursor"/>, and what was deleted. A null cursor asks for
    /// everything, which is what a device that has never synced needs.
    /// </summary>
    public async Task<ChangeFeedDto<NoteDto>> GetChangesAsync(string? cursor, CancellationToken cancellationToken = default)
    {
        var since = cursor ?? DateTimeOffset.MinValue.UtcDateTime.ToString("O");
        return await _httpClient.GetFromJsonAsync<ChangeFeedDto<NoteDto>>(
            $"api/notes/changes?since={Uri.EscapeDataString(since)}", cancellationToken)
            ?? new ChangeFeedDto<NoteDto>([], [], since);
    }

    /// <summary>The id the server assigned, which is what makes the local note reachable from anywhere else.</summary>
    public async Task<Guid> CreateAsync(CreateNoteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/notes", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken);
    }

    public async Task<WriteOutcome> UpdateAsync(Guid noteId, UpdateNoteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/notes/{noteId}", request, cancellationToken);
        return ReadOutcome(response);
    }

    /// <summary>
    /// Offers a copy to another account. The server records the offer; telling the recipient is this
    /// client's job, because the message that does it is end-to-end encrypted and only a client holds
    /// the key - see SharedItemSharing.
    /// </summary>
    public async Task<ShareResultDto?> ShareAsync(
        Guid noteId, Guid recipientUserId, string accessLevel, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/notes/{noteId}/shares", new { RecipientUserId = recipientUserId, AccessLevel = accessLevel },
            cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ShareResultDto>(cancellationToken)
            : null;
    }

    /// <summary>
    /// Turns an offer into a copy in this account's own notes. The offer itself arrived as a chat
    /// message; this is the half the server acts on - see SharedItemInvitation.
    /// </summary>
    public async Task<bool> AcceptShareAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync($"api/notes/shares/{shareId}/accept", null, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Its own endpoint rather than part of an update, because pinning is not a change to the note: it
    /// leaves UpdatedAtUtc alone, and only the owner may do it.
    /// </summary>
    public async Task<WriteOutcome> SetPinnedAsync(
        Guid noteId, bool isPinned, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"api/notes/{noteId}/pinned", new SetNotePinnedRequest(isPinned), cancellationToken);

        return ReadOutcome(response);
    }

    public async Task<WriteOutcome> DeleteAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/notes/{noteId}", cancellationToken);
        // A note already gone is the outcome the caller wanted, not a failure to retry.
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
            // A rule about the thing itself - see WriteOutcome.Rejected.
            case HttpStatusCode.BadRequest:
                return WriteOutcome.Rejected;
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
        using var response = await _httpClient.GetAsync($"api/notes/shares/{shareId}/status", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<bool>(cancellationToken)
            : null;
    }

    /// <summary>
    /// Claims this item while it is being edited, so a second editor is told rather than left to find
    /// out when their save is refused. Calling it again refreshes the claim - see EditLock.
    /// </summary>
    public Task<EditClaim> AcquireLockAsync(Guid serverId, CancellationToken cancellationToken = default)
        => EditLocking.AcquireAsync(_httpClient, $"api/notes/{serverId}/lock", cancellationToken);

    public Task ReleaseLockAsync(Guid serverId, CancellationToken cancellationToken = default)
        => EditLocking.ReleaseAsync(_httpClient, $"api/notes/{serverId}/lock", cancellationToken);
}
