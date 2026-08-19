using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Orbit.Core.Notifications;

namespace Orbit.Api.Notifications;

/// <summary>
/// Delivers a push notification to a real push service (whichever one the subscribing browser uses -
/// e.g. Mozilla's autopush, Google's FCM) via the WebPush package, which implements the VAPID
/// (RFC 8292) authentication and RFC 8291 message encryption a raw HTTP POST would otherwise have to
/// hand-roll - the push counterpart of SmtpEmailSender taking on MailKit for the same reason. Logs a
/// warning and does nothing when <see cref="VapidSettings"/> isn't configured, rather than throwing -
/// see that class's comment for why.
/// </summary>
public sealed class VapidPushNotificationSender : IPushNotificationSender
{
    private readonly IOptionsMonitor<VapidSettings> _settings;
    private readonly WebPush.WebPushClient _webPushClient;
    private readonly ILogger<VapidPushNotificationSender> _logger;

    public VapidPushNotificationSender(
        IOptionsMonitor<VapidSettings> settings, WebPush.WebPushClient webPushClient, ILogger<VapidPushNotificationSender> logger)
    {
        _settings = settings;
        _webPushClient = webPushClient;
        _logger = logger;
    }

    /// <summary>
    /// Throws <see cref="PushSubscriptionExpiredException"/> when the push service answers with 404 or
    /// 410 - see <see cref="IPushNotificationSender"/> for what the caller is expected to do with that.
    /// </summary>
    public async Task SendAsync(PushSubscription subscription, PushNotificationPayload payload, CancellationToken cancellationToken)
    {
        var currentSettings = _settings.CurrentValue;
        if (!currentSettings.IsConfigured)
        {
            _logger.LogWarning(
                "Vapid is not configured (see Vapid:PublicKeyBase64Url/PrivateKeyBase64Url/Subject) - " +
                "dropped a push notification to subscription {SubscriptionId}", subscription.Id);
            return;
        }

        var webPushSubscription = new WebPush.PushSubscription(subscription.Endpoint, subscription.P256dhBase64, subscription.AuthBase64);
        var vapidDetails = new WebPush.VapidDetails(
            currentSettings.Subject, currentSettings.PublicKeyBase64Url, currentSettings.PrivateKeyBase64Url);
        // Read by wwwroot/service-worker.js's "push" event handler in Orbit.Web - the property names
        // here and there must match.
        var payloadJson = JsonSerializer.Serialize(new { title = payload.Title, body = payload.Body, url = payload.Url });

        try
        {
            await _webPushClient.SendNotificationAsync(webPushSubscription, payloadJson, vapidDetails, cancellationToken);
        }
        catch (WebPush.WebPushException exception) when (exception.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            throw new PushSubscriptionExpiredException(
                $"Push service reported subscription {subscription.Id} as no longer valid ({(int)exception.StatusCode}).");
        }
    }
}
