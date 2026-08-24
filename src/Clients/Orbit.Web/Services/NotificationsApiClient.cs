using System.Net.Http.Json;
using Orbit.Contracts.Config;
using Orbit.Contracts.Notifications;

namespace Orbit.Web.Services;

/// <summary>
/// Thin wrapper around Orbit.Api's /api/notifications and /api/config/client-flags endpoints, keeping
/// HTTP and JSON details out of MainLayout/Options.razor.
/// </summary>
public sealed class NotificationsApiClient
{
    private readonly HttpClient _httpClient;

    public NotificationsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<NotificationSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<NotificationSettingsDto>("api/notifications/settings", cancellationToken)
            ?? new NotificationSettingsDto(true, true, true, true, true, BannerVisibleSeconds: 5, BannerMinimumGapSeconds: 5);

    public async Task<NotificationSettingsDto> UpdateSettingsAsync(
        UpdateNotificationSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync("api/notifications/settings", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NotificationSettingsDto>(cancellationToken: cancellationToken)
            ?? new NotificationSettingsDto(true, true, true, true, true, BannerVisibleSeconds: 5, BannerMinimumGapSeconds: 5);
    }

    public async Task<IReadOnlyList<NotificationEntryDto>> GetRecentAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<NotificationEntryDto>>("api/notifications", cancellationToken) ?? [];

    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<UnreadCountDto>("api/notifications/unread-count", cancellationToken);
        return response?.Count ?? 0;
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync("api/notifications/read", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Unauthenticated - see ConfigEndpoints.MapConfigEndpoints. Defaults to "not allowed" if the call itself fails, matching the fail-closed intent of the flag.</summary>
    public async Task<bool> GetExceptionDetailsAllowedAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<ClientFlagsDto>("api/config/client-flags", cancellationToken);
        return response?.ExceptionDetailsAllowed ?? false;
    }
}
