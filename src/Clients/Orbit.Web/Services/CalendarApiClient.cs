using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Calendar;
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
    private readonly ILogger _logger;

    // logger defaults to a no-op instance rather than being required, so existing call sites (including
    // every test that constructs this with just an HttpClient) keep compiling unchanged; only the
    // DI-resolved instance registered in Program.cs actually logs anywhere.
    public CalendarApiClient(HttpClient httpClient, ILogger<CalendarApiClient>? logger = null)
    {
        _httpClient = httpClient;
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

    public async Task UpdateCalendarEventAsync(Guid id, UpdateCalendarEventRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/calendar-events/{id}", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            _logger.LogActionCompleted(ClientActionCategory.Edit, "Update calendar event");
        }
        catch (Exception exception)
        {
            _logger.LogActionFailed(ClientActionCategory.Edit, "Update calendar event", exception);
            throw;
        }
    }

    public async Task DeleteCalendarEventAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/calendar-events/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Offers a read-only copy of calendarEventId to recipientUserId, returning the new share's id, or
    /// null if calendarEventId doesn't exist or isn't owned by the caller. Notifying the recipient (an
    /// encrypted chat message carrying that id) is a separate step - see EncryptedChatMessageSender.
    /// </summary>
    public async Task<Guid?> ShareCalendarEventAsync(Guid calendarEventId, Guid recipientUserId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"api/calendar-events/{calendarEventId}/shares", new ShareCalendarEventRequest(recipientUserId), cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var shareId = await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
            _logger.LogActionCompleted(ClientActionCategory.ShareElement, "Share calendar event");
            return shareId;
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
}
