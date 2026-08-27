using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Sharing;
using Orbit.Core.Abstractions;
using Orbit.Web.Services.Logging;

namespace Orbit.Web.Services;

/// <summary>
/// Thin wrapper around Orbit.Api's /api/calendar-events endpoints, keeping HTTP and JSON details out of
/// the pages.
/// </summary>
public sealed class CalendarApiClient
{
    private readonly HttpClient _httpClient;
    private readonly Translations? _translations;
    private readonly ILogger _logger;

    // logger defaults to a no-op instance and translations to absent rather than being required, so
    // existing call sites (including every test that constructs this with just an HttpClient) keep
    // compiling unchanged; only the DI-resolved instance registered in Program.cs actually logs
    // anywhere or speaks the reader's language.
    public CalendarApiClient(HttpClient httpClient, ILogger<CalendarApiClient>? logger = null, Translations? translations = null)
    {
        _httpClient = httpClient;
        _translations = translations;
        _logger = logger ?? NullLogger<CalendarApiClient>.Instance;
    }

    public async Task<IReadOnlyList<CalendarEventDto>> GetCalendarEventsAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<CalendarEventDto>>("api/calendar-events", cancellationToken) ?? [];

    public async Task<CalendarEventDto?> GetCalendarEventByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/calendar-events/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CalendarEventDto>(cancellationToken: cancellationToken);
    }

    public async Task<Guid> CreateCalendarEventAsync(CreateCalendarEventRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/calendar-events", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var id = await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
            _logger.LogActionCompleted(ClientActionCategory.Save, "Create calendar event");
            return id;
        }
        catch (Exception exception)
        {
            _logger.LogActionFailed(ClientActionCategory.Save, "Create calendar event", exception);
            throw;
        }
    }

    /// <summary>Mirrors NotesApiClient.UpdateNoteAsync - see its comment for what NotFound/Locked mean here.</summary>
    public async Task<EditOutcome> UpdateCalendarEventAsync(Guid id, UpdateCalendarEventRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/calendar-events/{id}", request, cancellationToken);
            var outcome = await ToEditOutcomeAsync(response, cancellationToken);
            if (outcome.Kind == EditOutcomeKind.Success)
            {
                _logger.LogActionCompleted(ClientActionCategory.Edit, "Update calendar event");
            }

            return outcome;
        }
        catch (Exception exception)
        {
            _logger.LogActionFailed(ClientActionCategory.Edit, "Update calendar event", exception);
            throw;
        }
    }

    /// <summary>Mirrors NotesApiClient.AcquireNoteLockAsync - see its comment.</summary>
    public async Task<EditOutcome> AcquireCalendarEventLockAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/calendar-events/{id}/lock", content: null, cancellationToken);
        return await ToEditOutcomeAsync(response, cancellationToken);
    }

    /// <summary>Mirrors NotesApiClient.ReleaseNoteLockAsync - see its comment.</summary>
    public async Task ReleaseCalendarEventLockAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/calendar-events/{id}/lock", cancellationToken);
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

    public async Task DeleteCalendarEventAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/calendar-events/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Offers a copy of calendarEventId to recipientUserId under the given access level ("ReadOnly",
    /// "Share", or "CanEdit" - see Orbit.Core.Abstractions.ShareAccessLevel), or null if calendarEventId
    /// doesn't exist, isn't accessible to the caller, or the caller isn't allowed to share it (see
    /// ShareCalendarEventCommandHandler). <see cref="ShareResultDto.AlreadyShared"/> is true when
    /// calendarEventId was already offered to recipientUserId - the returned ShareId is the existing
    /// offer's, not a new one, so the caller can send it again as a reminder instead of creating a
    /// duplicate. Notifying the recipient (an encrypted chat message carrying that id) is a separate
    /// step - see EncryptedChatMessageSender.
    /// </summary>
    public async Task<ShareResultDto?> ShareCalendarEventAsync(
        Guid calendarEventId, Guid recipientUserId, string accessLevel = "ReadOnly", CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"api/calendar-events/{calendarEventId}/shares", new ShareCalendarEventRequest(recipientUserId, accessLevel), cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ShareResultDto>(cancellationToken: cancellationToken);
            _logger.LogActionCompleted(ClientActionCategory.ShareElement, "Share calendar event");
            return result;
        }
        catch (Exception exception)
        {
            _logger.LogActionFailed(ClientActionCategory.ShareElement, "Share calendar event", exception);
            throw;
        }
    }

    /// <summary>Returns false instead of throwing when shareId doesn't exist or wasn't offered to the caller.</summary>
    public async Task<bool> AcceptCalendarEventShareAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/calendar-events/shares/{shareId}/accept", content: null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>Whether shareId has already been accepted, or null if it doesn't exist or wasn't offered to the caller.</summary>
    public async Task<bool?> GetCalendarEventShareStatusAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/calendar-events/shares/{shareId}/status", cancellationToken);
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
