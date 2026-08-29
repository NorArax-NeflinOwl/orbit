namespace Orbit.Core.Notifications;

/// <summary>
/// Delivers a single push message to one subscription. Implemented outside Orbit.Core - see
/// VapidPushNotificationSender for browsers and FirebasePushNotificationSender for the mobile apps - so
/// domain and application logic never depends on a specific push library or transport, the same
/// separation <see cref="IEmailSender"/> gives calendar event reminder emails.
/// </summary>
public interface IPushNotificationSender
{
    /// <summary>
    /// Which kind of subscription this sender can deliver to. PushNotificationDispatcher picks by this
    /// rather than by type, so adding a transport is registering another implementation.
    /// </summary>
    PushTransport Transport { get; }

    /// <summary>
    /// Throws <see cref="PushSubscriptionExpiredException"/> when the push service reports
    /// <paramref name="subscription"/> as no longer valid (HTTP 404/410) - the caller (see
    /// <see cref="PushNotificationDispatcher"/>) is expected to delete it in that case, rather than
    /// retrying it again on the next notification.
    /// </summary>
    Task SendAsync(PushSubscription subscription, PushNotificationPayload payload, CancellationToken cancellationToken);
}
