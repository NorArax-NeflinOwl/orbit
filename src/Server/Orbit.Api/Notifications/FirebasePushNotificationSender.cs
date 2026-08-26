using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Orbit.Core.Notifications;

namespace Orbit.Api.Notifications;

/// <summary>
/// Delivers a push notification to an Orbit.Maui app through Firebase Cloud Messaging - the mobile
/// counterpart of <see cref="VapidPushNotificationSender"/>, and registered alongside it so
/// PushNotificationDispatcher can pick by transport.
///
/// One transport covers both apps: FCM reaches Android directly and iOS through APNs underneath, which
/// is why there is no separate APNs sender here. That does mean iOS delivery depends on an APNs key
/// being uploaded to the Firebase console - without it FCM accepts the send and the message simply
/// never arrives, so that failure is called out explicitly below rather than left silent.
///
/// Logs a warning and does nothing when <see cref="FirebaseSettings"/> isn't configured, exactly as the
/// VAPID sender does - a fresh checkout should still run without Firebase set up.
/// </summary>
public sealed class FirebasePushNotificationSender : IPushNotificationSender
{
    private readonly IOptionsMonitor<FirebaseSettings> _settings;
    private readonly FirebaseAccessTokenProvider _accessTokenProvider;
    private readonly HttpClient _httpClient;
    private readonly ILogger<FirebasePushNotificationSender> _logger;

    public FirebasePushNotificationSender(
        IOptionsMonitor<FirebaseSettings> settings, FirebaseAccessTokenProvider accessTokenProvider,
        HttpClient httpClient, ILogger<FirebasePushNotificationSender> logger)
    {
        _settings = settings;
        _accessTokenProvider = accessTokenProvider;
        _httpClient = httpClient;
        _logger = logger;
    }

    public PushTransport Transport => PushTransport.Firebase;

    /// <summary>
    /// Throws <see cref="PushSubscriptionExpiredException"/> when FCM reports the registration token as
    /// no longer valid, which is how an uninstalled app or a reset device surfaces - see
    /// <see cref="IPushNotificationSender"/> for what the caller does with that.
    /// </summary>
    public async Task SendAsync(PushSubscription subscription, PushNotificationPayload payload, CancellationToken cancellationToken)
    {
        if (!_settings.CurrentValue.IsConfigured)
        {
            _logger.LogWarning(
                "Firebase is not configured (see Firebase:ServiceAccountKeyPath) - dropped a push " +
                "notification to subscription {SubscriptionId}", subscription.Id);
            return;
        }

        if (subscription.Device is not { } device)
        {
            _logger.LogError("Subscription {SubscriptionId} claims Firebase but carries no device token", subscription.Id);
            return;
        }

        var accessToken = await _accessTokenProvider.GetAccessTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{_accessTokenProvider.ReadProjectId()}/messages:send")
        {
            Content = new StringContent(BuildMessageJson(device, payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (IsTokenNoLongerValid(response.StatusCode, body))
        {
            throw new PushSubscriptionExpiredException(
                $"Firebase reported subscription {subscription.Id}'s device token as no longer valid ({(int)response.StatusCode}).");
        }

        if (IsApnsKeyMissing(response.StatusCode, body))
        {
            // Worth naming rather than leaving inside a generic refusal: nothing in this repository can
            // fix it, and reading it as "Firebase refused something" sends whoever sees it looking in
            // the wrong place. The key is uploaded in the Firebase console, not configured here.
            throw new InvalidOperationException(
                "Firebase cannot reach iOS: no valid APNs key is uploaded for this project. Upload the APNs "
                + $"auth key under Project settings > Cloud Messaging in the Firebase console. ({Summarise(body)})");
        }

        throw new InvalidOperationException(
            $"Firebase refused a push notification ({(int)response.StatusCode}): {Summarise(body)}");
    }

    /// <summary>
    /// The FCM v1 message. "notification" is what both platforms display; the per-platform blocks carry
    /// the tap target, because Android reads it from data and iOS needs it in the aps payload's custom
    /// fields rather than anywhere shared.
    /// </summary>
    private static string BuildMessageJson(DeviceRegistration device, PushNotificationPayload payload)
        => JsonSerializer.Serialize(new
        {
            message = new
            {
                token = device.Token,
                notification = new { title = payload.Title, body = payload.Body },
                data = new { url = payload.Url },
                apns = new
                {
                    payload = new { aps = new { sound = "default" }, url = payload.Url }
                }
            }
        });

    /// <summary>
    /// FCM reports a dead token as UNREGISTERED, and a malformed one as INVALID_ARGUMENT. Both mean the
    /// subscription will never work again, so both are worth pruning rather than retrying forever.
    /// </summary>
    /// <summary>
    /// FCM's answer when a message is addressed to an iOS device and the project has no usable APNs
    /// key. Only covers the case FCM notices: a key that is present but wrong for the bundle leaves FCM
    /// accepting the send and the message dying at Apple, which nothing here can observe - see the
    /// deployment notes on why that one is a checklist item rather than a check.
    /// </summary>
    internal static bool IsApnsKeyMissing(HttpStatusCode statusCode, string body)
        => statusCode == HttpStatusCode.Unauthorized
            && body.Contains("THIRD_PARTY_AUTH_ERROR", StringComparison.OrdinalIgnoreCase);

    private static bool IsTokenNoLongerValid(HttpStatusCode statusCode, string body)
        => statusCode == HttpStatusCode.NotFound
            || (statusCode == HttpStatusCode.BadRequest && body.Contains("registration token", StringComparison.OrdinalIgnoreCase))
            || body.Contains("UNREGISTERED", StringComparison.Ordinal);

    private static string Summarise(string body) => body.Length <= 300 ? body : body[..300];
}
