namespace Orbit.Core.Notifications;

/// <summary>
/// Delivers a single Web Push message to one subscription. Implemented against a real push service over
/// HTTP with VAPID authentication outside Orbit.Core (see VapidPushNotificationSender in Orbit.Api), so
/// domain and application logic never depends on a specific push library or transport - the same
/// separation <see cref="IEmailSender"/> gives calendar event reminder emails.
/// </summary>
public interface IPushNotificationSender
{
    /// <summary>
    /// Throws <see cref="PushSubscriptionExpiredException"/> when the push service reports
    /// <paramref name="subscription"/> as no longer valid (HTTP 404/410) - the caller (see
    /// <see cref="PushNotificationDispatcher"/>) is expected to delete it in that case, rather than
    /// retrying it again on the next notification.
    /// </summary>
    Task SendAsync(PushSubscription subscription, PushNotificationPayload payload, CancellationToken cancellationToken);
}
