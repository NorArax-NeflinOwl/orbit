using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Sharing;
using Orbit.Core.Abstractions;
using Orbit.Web.Services.Logging;

namespace Orbit.Web.Services;

/// <summary>
/// Thin wrapper around Orbit.Api's /api/notes endpoints, keeping HTTP and JSON details out of the pages.
/// </summary>
public sealed class NotesApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    // logger defaults to a no-op instance rather than being required, so existing call sites (including
    // every test that constructs this with just an HttpClient) keep compiling unchanged; only the
    // DI-resolved instance registered in Program.cs actually logs anywhere.
    public NotesApiClient(HttpClient httpClient, ILogger<NotesApiClient>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger ?? NullLogger<NotesApiClient>.Instance;
    }

    public async Task<IReadOnlyList<NoteDto>> GetNotesAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<NoteDto>>("api/notes", cancellationToken) ?? [];

    public async Task<NoteDto?> GetNoteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/notes/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NoteDto>(cancellationToken: cancellationToken);
    }

    public async Task<Guid> CreateNoteAsync(CreateNoteRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/notes", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var id = await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
            _logger.LogActionCompleted(ClientActionCategory.Save, "Create note");
            return id;
        }
        catch (Exception exception)
        {
            _logger.LogActionFailed(ClientActionCategory.Save, "Create note", exception);
            throw;
        }
    }

    /// <summary>
    /// NotFound covers the note being missing, not accessible, or accessible only at ReadOnly/Share (no
    /// edit rights) - the API 404s for all three, without telling the client which one applies. Locked
    /// means someone else currently holds the edit lock - see AcquireNoteLockAsync, which
    /// NoteEditor.razor is expected to have already called successfully before ever reaching this point,
    /// so getting Locked back here means the lock was taken or expired out from under it mid-edit.
    /// </summary>
    public async Task<EditOutcome> UpdateNoteAsync(Guid id, UpdateNoteRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/notes/{id}", request, cancellationToken);
            var outcome = await ToEditOutcomeAsync(response, cancellationToken);
            if (outcome.Kind == EditOutcomeKind.Success)
            {
                _logger.LogActionCompleted(ClientActionCategory.Edit, "Update note");
            }

            return outcome;
        }
        catch (Exception exception)
        {
            _logger.LogActionFailed(ClientActionCategory.Edit, "Update note", exception);
            throw;
        }
    }

    public async Task DeleteNoteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/notes/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Acquires (or refreshes, if this browser already holds it) the edit lock on noteId - see
    /// AcquireNoteLockCommand. NoteEditor.razor calls this once when opening a note it has CanEdit
    /// access to, then again on a heartbeat while the editor stays open.
    /// </summary>
    public async Task<EditOutcome> AcquireNoteLockAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/notes/{id}/lock", content: null, cancellationToken);
        return await ToEditOutcomeAsync(response, cancellationToken);
    }

    /// <summary>Releases this browser's own edit lock on noteId, if it holds one - a no-op otherwise.</summary>
    public async Task ReleaseNoteLockAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/notes/{id}/lock", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<EditOutcome> ToEditOutcomeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return EditOutcome.NotFound;
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var conflict = await response.Content.ReadFromJsonAsync<LockConflictDto>(cancellationToken: cancellationToken);
            return EditOutcome.LockedBy(conflict?.LockedByUserName ?? "another user");
        }

        response.EnsureSuccessStatusCode();
        return EditOutcome.Success;
    }

    /// <summary>
    /// Offers a copy of noteId to recipientUserId under the given access level ("ReadOnly", "Share", or
    /// "CanEdit" - see Orbit.Core.Abstractions.ShareAccessLevel), or null if noteId doesn't exist, isn't
    /// accessible to the caller, or the caller isn't allowed to share it (see ShareNoteCommandHandler).
    /// <see cref="ShareResultDto.AlreadyShared"/> is true when noteId was already offered to
    /// recipientUserId - the returned ShareId is the existing offer's, not a new one, so the caller can
    /// send it again as a reminder instead of creating a duplicate. Notifying the recipient (an
    /// encrypted chat message carrying that id) is a separate step - see EncryptedChatMessageSender.
    /// Mirrors CalendarApiClient.ShareCalendarEventAsync.
    /// </summary>
    public async Task<ShareResultDto?> ShareNoteAsync(
        Guid noteId, Guid recipientUserId, string accessLevel = "ReadOnly", CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"api/notes/{noteId}/shares", new ShareNoteRequest(recipientUserId, accessLevel), cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ShareResultDto>(cancellationToken: cancellationToken);
            _logger.LogActionCompleted(ClientActionCategory.ShareElement, "Share note");
            return result;
        }
        catch (Exception exception)
        {
            _logger.LogActionFailed(ClientActionCategory.ShareElement, "Share note", exception);
            throw;
        }
    }

    /// <summary>Returns false instead of throwing when shareId doesn't exist or wasn't offered to the caller.</summary>
    public async Task<bool> AcceptNoteShareAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/notes/shares/{shareId}/accept", content: null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>Whether shareId has already been accepted, or null if it doesn't exist or wasn't offered to the caller.</summary>
    public async Task<bool?> GetNoteShareStatusAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/notes/shares/{shareId}/status", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken);
    }
}
