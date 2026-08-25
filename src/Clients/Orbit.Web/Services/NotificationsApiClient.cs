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

    /// <summary>The unread entries themselves - the caller derives both the avatar count and the per-source badges from them.</summary>
    public async Task<IReadOnlyList<NotificationEntryDto>> GetUnreadAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<NotificationEntryDto>>("api/notifications/unread", cancellationToken) ?? [];

    /// <summary>Empties the feed, as opposed to MarkAllReadAsync which only clears the unread state.</summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync("api/notifications", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync("api/notifications/read", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
