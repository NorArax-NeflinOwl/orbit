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

    public async Task UpdateNoteAsync(Guid id, UpdateNoteRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/notes/{id}", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            _logger.LogActionCompleted(ClientActionCategory.Edit, "Update note");
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
