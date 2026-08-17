using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Calendar;

namespace Orbit.Web.Services;

/// <summary>
/// Thin wrapper around Orbit.Api's /api/calendar-events endpoints, keeping HTTP and JSON details out of
/// the pages.
/// </summary>
public sealed class CalendarApiClient
{
    private readonly HttpClient _httpClient;

    public CalendarApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
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
        var response = await _httpClient.PostAsJsonAsync("api/calendar-events", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateCalendarEventAsync(Guid id, UpdateCalendarEventRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/calendar-events/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteCalendarEventAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/calendar-events/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
