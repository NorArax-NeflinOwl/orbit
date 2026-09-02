using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Sync;
using Orbit.Contracts.Sharing;

namespace Orbit.Mobile.Api;

/// <summary>
/// The calendar half of the API. Only the synchroniser calls this - screens read the local database.
/// </summary>
public sealed class CalendarClient : ILockableItems
{
    private readonly HttpClient _httpClient;

    public CalendarClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<ChangeFeedDto<CalendarEventDto>> GetChangesAsync(
        string? cursor, CancellationToken cancellationToken = default)
    {
        var since = cursor ?? DateTimeOffset.MinValue.UtcDateTime.ToString("O");
        return await _httpClient.GetFromJsonAsync<ChangeFeedDto<CalendarEventDto>>(
            $"api/calendar-events/changes?since={Uri.EscapeDataString(since)}", cancellationToken)
            ?? new ChangeFeedDto<CalendarEventDto>([], [], since);
    }

    public async Task<Guid> CreateAsync(CreateCalendarEventRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/calendar-events", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken);
    }

    /// <summary>
    /// Offers a copy to another account. The server records the offer; telling the recipient is this
    /// client's job, because the message that does it is end-to-end encrypted and only a client holds
    /// the key - see SharedItemSharing.
    /// </summary>
    public async Task<ShareResultDto?> ShareAsync(
        Guid calendarEventId, Guid recipientUserId, string accessLevel, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/calendar-events/{calendarEventId}/shares", new { RecipientUserId = recipientUserId, AccessLevel = accessLevel },
            cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ShareResultDto>(cancellationToken)
            : null;
    }

    /// <inheritdoc cref="NotesClient.AcceptShareAsync"/>
    public async Task<bool> AcceptShareAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync($"api/calendar-events/shares/{shareId}/accept", null, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<WriteOutcome> UpdateAsync(
        Guid calendarEventId, UpdateCalendarEventRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/calendar-events/{calendarEventId}", request, cancellationToken);
        return ReadOutcome(response);
    }

    public async Task<WriteOutcome> DeleteAsync(Guid calendarEventId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/calendar-events/{calendarEventId}", cancellationToken);
        // An event already gone is the outcome the caller wanted, not a failure to retry.
        return response.StatusCode is HttpStatusCode.NotFound ? WriteOutcome.Applied : ReadOutcome(response);
    }

    /// <summary>Anything not named here throws, so the queued change stays queued and is tried again.</summary>
    private static WriteOutcome ReadOutcome(HttpResponseMessage response)
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.Conflict:
            // Not this reader's to change - a share that was read-only, or has become so. Answered
            // rather than thrown for the reason WriteOutcome.Refused gives.
            case HttpStatusCode.Forbidden:
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
        using var response = await _httpClient.GetAsync($"api/calendar-events/shares/{shareId}/status", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<bool>(cancellationToken)
            : null;
    }

    /// <summary>
    /// Claims this item while it is being edited, so a second editor is told rather than left to find
    /// out when their save is refused. Calling it again refreshes the claim - see EditLock.
    /// </summary>
    public Task<EditClaim> AcquireLockAsync(Guid serverId, CancellationToken cancellationToken = default)
        => EditLocking.AcquireAsync(_httpClient, $"api/calendar-events/{serverId}/lock", cancellationToken);

    public Task ReleaseLockAsync(Guid serverId, CancellationToken cancellationToken = default)
        => EditLocking.ReleaseAsync(_httpClient, $"api/calendar-events/{serverId}/lock", cancellationToken);
}
