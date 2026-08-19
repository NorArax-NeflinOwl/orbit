using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.PushNotifications;

namespace Orbit.Web.Services;

/// <summary>
/// Thin wrapper around Orbit.Api's /api/push endpoints, keeping HTTP and JSON details out of
/// PushNotificationManager.
/// </summary>
public sealed class PushNotificationApiClient
{
    private readonly HttpClient _httpClient;

    public PushNotificationApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GetVapidPublicKeyAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<PushPublicKeyDto>("api/push/public-key", cancellationToken);
        return response?.PublicKeyBase64Url ?? string.Empty;
    }

    public async Task SubscribeAsync(
        string endpoint, string p256dhBase64, string authBase64, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/push/subscriptions", new PushSubscriptionRequest(endpoint, p256dhBase64, authBase64), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// A 404 (Orbit.Api never had this endpoint on file - e.g. it was already removed by an earlier
    /// call) is treated the same as success, since either way the end state the caller wants - no
    /// subscription for this endpoint - already holds.
    /// </summary>
    public async Task UnsubscribeAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync(
            $"api/push/subscriptions?endpoint={Uri.EscapeDataString(endpoint)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }
}
