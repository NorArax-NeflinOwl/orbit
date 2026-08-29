using System.Net.Http.Json;
using Orbit.Contracts.Notifications;
using Orbit.Contracts.PushNotifications;

namespace Orbit.Mobile.Api;

/// <summary>
/// The notification half of the API: the feed the app shows, the settings behind it, and this device's
/// push registration.
///
/// Deliberately not cached in the local store, unlike notes or messages. A notification feed read while
/// offline would be a list of things to tap that cannot be opened and cannot be marked read - the
/// screen says it needs a connection instead, which is both simpler and truer.
/// </summary>
public sealed class NotificationsClient
{
    private readonly HttpClient _httpClient;

    public NotificationsClient(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>The recent feed, newest first, capped by the server.</summary>
    public async Task<IReadOnlyList<NotificationEntryDto>> GetRecentAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<NotificationEntryDto>>(
            "api/notifications", cancellationToken) ?? [];

    /// <summary>Everything still held, cleared entries included - what the reader searches when something was tidied away too soon.</summary>
    public async Task<IReadOnlyList<NotificationEntryDto>> GetHistoryAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<NotificationEntryDto>>(
            "api/notifications/history", cancellationToken) ?? [];

    public async Task<IReadOnlyList<NotificationEntryDto>> GetUnreadAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<NotificationEntryDto>>(
            "api/notifications/unread", cancellationToken) ?? [];

    public async Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync("api/notifications/read", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Marks whatever pointed at this path as read. Arriving at the screen a notification was about
    /// counts as having read it, however the reader got there - so the screens call this, not just a tap
    /// on the feed.
    /// </summary>
    public async Task MarkReadAtAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/notifications/read-at", new MarkNotificationsReadAtUrlRequest(url), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Tidies the feed away. The entries survive on the history until the retention window deletes them.</summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync("api/notifications", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<NotificationSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<NotificationSettingsDto>("api/notifications/settings", cancellationToken)
            ?? throw new InvalidOperationException("The server returned no notification settings.");

    public async Task<NotificationSettingsDto> SaveSettingsAsync(
        UpdateNotificationSettingsRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync("api/notifications/settings", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NotificationSettingsDto>(cancellationToken)
            ?? throw new InvalidOperationException("The server accepted the settings but returned none back.");
    }

    /// <summary>
    /// Registers this device's push token. Sent on every sign-in rather than once: a token is not
    /// permanent - it changes when the app is reinstalled or its data cleared - and the server replaces
    /// the row for a token it already holds, so repeating is cheap and forgetting is not.
    /// </summary>
    public async Task RegisterDeviceAsync(
        string deviceToken, string platform, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/push/device-subscriptions", new DevicePushSubscriptionRequest(deviceToken, platform), cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
