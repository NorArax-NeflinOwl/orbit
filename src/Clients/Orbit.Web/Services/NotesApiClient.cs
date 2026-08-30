using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Notes;
using Orbit.Contracts;
using Orbit.Contracts.Sharing;
using Orbit.Core.Abstractions;
using Orbit.Web.Services.Logging;

namespace Orbit.Web.Services;

/// <summary>
/// Thin wrapper around Orbit.Api's /api/notes endpoints, keeping HTTP and JSON details out of the pages.
///
/// Private notes are sealed and opened here rather than in each page (see PrivateContentSealer): every
/// caller gets a NoteDto with a readable title and content whichever kind of note it is, and none of
/// them has to remember that a private one arrives empty with its real content encrypted.
/// </summary>
public sealed class NotesApiClient
{
    private readonly HttpClient _httpClient;
    private readonly Translations? _translations;
    private readonly ILogger _logger;
    private readonly PrivateContentSealer? _privateContentSealer;

    /// <summary>Shown in place of a private note nobody can open any more - see PrivateContentSealer.OpenAsync.</summary>
    public const string UnreadableNoteTitle = "Unreadable - encrypted with an older key";

    // logger, sealer and translations default to absent rather than being required, so existing call
    // sites (including every test that constructs this with just an HttpClient) keep compiling
    // unchanged; only the DI-resolved instance registered in Program.cs logs, handles private notes, or
    // speaks the reader's language.
    public NotesApiClient(HttpClient httpClient, ILogger<NotesApiClient>? logger = null, PrivateContentSealer? privateContentSealer = null, Translations? translations = null)
    {
        _httpClient = httpClient;
        _translations = translations;
        _logger = logger ?? NullLogger<NotesApiClient>.Instance;
        _privateContentSealer = privateContentSealer;
    }

    public async Task<IReadOnlyList<NoteDto>> GetNotesAsync(CancellationToken cancellationToken = default)
    {
        var notes = await _httpClient.GetFromJsonAsync<List<NoteDto>>("api/notes", cancellationToken) ?? [];

        var opened = new List<NoteDto>(notes.Count);
        foreach (var note in notes)
        {
            opened.Add(await OpenIfPrivateAsync(note, cancellationToken));
        }

        return opened;
    }

    public async Task<NoteDto?> GetNoteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/notes/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var note = await response.Content.ReadFromJsonAsync<NoteDto>(cancellationToken: cancellationToken);
        return note is null ? null : await OpenIfPrivateAsync(note, cancellationToken);
    }

    /// <summary>
    /// Hands back an ordinary note unchanged, and a private one with its real title and content put back
    /// in place. A note that can't be opened keeps its empty content and says so in the title rather
    /// than throwing, so one unreadable note doesn't take a whole list down with it.
    /// </summary>
    private async Task<NoteDto> OpenIfPrivateAsync(NoteDto note, CancellationToken cancellationToken)
    {
        if (!note.IsPrivate || note.EncryptedContent is not { } encryptedContent || _privateContentSealer is null)
        {
            return note;
        }

        var content = await _privateContentSealer.OpenAsync<SealedNote>(encryptedContent, cancellationToken);
        return content is null
            ? note with { Title = Translated(UnreadableNoteTitle) }
            : note with { Title = content.Title, Content = content.Content };
    }

    /// <summary>
    /// Seals a private note's title and content and empties the readable fields, so what leaves this
    /// browser matches what the server is allowed to hold. Left alone when the note isn't private.
    /// </summary>
    private async Task<(string Title, IReadOnlyList<NoteContentLineDto> Content, EncryptedContentDto? EncryptedContent)> SealIfPrivateAsync(
        string title, IReadOnlyList<NoteContentLineDto> content, bool isPrivate, CancellationToken cancellationToken)
    {
        if (!isPrivate)
        {
            return (title, content, null);
        }

        if (_privateContentSealer is null)
        {
            throw new InvalidOperationException("This NotesApiClient was built without a PrivateContentSealer, so it can't save a private note.");
        }

        var encryptedContent = await _privateContentSealer.SealAsync(new SealedNote(title, content), cancellationToken);
        return (string.Empty, [], encryptedContent);
    }

    public async Task<Guid> CreateNoteAsync(CreateNoteRequest request, CancellationToken cancellationToken = default)
    {
        var (title, content, encryptedContent) = await SealIfPrivateAsync(
            request.Title, request.Content, request.IsPrivate, cancellationToken);
        request = request with { Title = title, Content = content, EncryptedContent = encryptedContent };

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
        var (title, content, encryptedContent) = await SealIfPrivateAsync(
            request.Title, request.Content, request.IsPrivate, cancellationToken);
        request = request with { Title = title, Content = content, EncryptedContent = encryptedContent };

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

    public async Task<bool> SetPinnedAsync(Guid noteId, bool isPinned, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"api/notes/{noteId}/pinned", new SetNotePinnedRequest(isPinned), cancellationToken);
        return response.IsSuccessStatusCode;
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

    /// <summary>
    /// The reader's language for text this client substitutes in - English when there is no
    /// Translations to ask, which is every test that builds this client by hand. Translated here
    /// rather than where it is rendered, because by then a stand-in title is indistinguishable from
    /// a real one the reader wrote.
    /// </summary>
    private string Translated(string english) => _translations?[english] ?? english;
}
