namespace Orbit.Core.Notifications;

/// <summary>
/// How a given subscription is reached. A browser and a phone are not the same kind of destination:
/// Web Push is a POST to a per-subscription URL the browser handed out, while a mobile app is a
/// registration token FCM resolves. Neither shape fits the other's fields, so the transport says which
/// half of <see cref="PushSubscription"/> is filled in.
/// </summary>
public enum PushTransport
{
    WebPush,

    /// <summary>Firebase Cloud Messaging, covering both mobile apps - Android directly, iOS via APNs underneath.</summary>
    Firebase
}
