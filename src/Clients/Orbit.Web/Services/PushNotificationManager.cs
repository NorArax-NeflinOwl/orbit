using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// Orchestrates browser push notifications: whether this browser can support them at all, whether it
/// currently has a subscription, and turning that subscription on (asking for permission, subscribing via
/// wwwroot/js/pushNotifications.js, then registering the subscription with Orbit.Api) or off. MainLayout
/// exposes this as a single "enable/disable push notifications" control.
/// </summary>
public sealed class PushNotificationManager
{
    private readonly IJSRuntime _jsRuntime;
    private readonly PushNotificationApiClient _pushNotificationApiClient;

    public PushNotificationManager(IJSRuntime jsRuntime, PushNotificationApiClient pushNotificationApiClient)
    {
        _jsRuntime = jsRuntime;
        _pushNotificationApiClient = pushNotificationApiClient;
    }

    /// <summary>False when the browser lacks service workers, the Push API, or the Notification API.</summary>
    public async Task<bool> IsSupportedAsync()
    {
        await using var pushModule = await ImportModuleAsync();
        return await pushModule.InvokeAsync<bool>("isSupported");
    }

    /// <summary>Whether this browser currently has an active push subscription.</summary>
    public async Task<bool> IsEnabledAsync()
    {
        await using var pushModule = await ImportModuleAsync();
        var endpoint = await pushModule.InvokeAsync<string?>("getExistingSubscriptionEndpoint");
        return !string.IsNullOrEmpty(endpoint);
    }

    /// <summary>
    /// Asks the browser for notification permission and, if granted, subscribes and registers the
    /// subscription with Orbit.Api. Returns false without registering anything if the user denies
    /// permission, or if Orbit.Api has no VAPID public key configured (see VapidSettings).
    /// </summary>
    public async Task<bool> EnableAsync(CancellationToken cancellationToken = default)
    {
        var vapidPublicKeyBase64Url = await _pushNotificationApiClient.GetVapidPublicKeyAsync(cancellationToken);
        if (string.IsNullOrEmpty(vapidPublicKeyBase64Url))
        {
            return false;
        }

        await using var pushModule = await ImportModuleAsync();
        var subscription = await pushModule.InvokeAsync<BrowserPushSubscription?>(
            "requestPermissionAndSubscribe", vapidPublicKeyBase64Url);
        if (subscription is null)
        {
            return false;
        }

        await _pushNotificationApiClient.SubscribeAsync(
            subscription.Endpoint, subscription.P256dhBase64, subscription.AuthBase64, cancellationToken);
        return true;
    }

    /// <summary>Cancels this browser's push subscription, both locally and on Orbit.Api.</summary>
    public async Task DisableAsync(CancellationToken cancellationToken = default)
    {
        await using var pushModule = await ImportModuleAsync();
        var endpoint = await pushModule.InvokeAsync<string?>("unsubscribe");
        if (!string.IsNullOrEmpty(endpoint))
        {
            await _pushNotificationApiClient.UnsubscribeAsync(endpoint, cancellationToken);
        }
    }

    private async Task<IJSObjectReference> ImportModuleAsync()
        => await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/pushNotifications.js");

    /// <summary>Shape returned by pushNotifications.js's requestPermissionAndSubscribe - marshalled by JS interop.</summary>
    private sealed record BrowserPushSubscription(string Endpoint, string P256dhBase64, string AuthBase64);
}
