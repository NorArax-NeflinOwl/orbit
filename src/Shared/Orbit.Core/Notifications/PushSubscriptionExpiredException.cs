namespace Orbit.Core.Notifications;

/// <summary>
/// Thrown by <see cref="IPushNotificationSender"/> when the push service reports a subscription as
/// permanently gone (HTTP 404/410) - e.g. the user uninstalled the browser, cleared its storage, or
/// explicitly revoked the permission outside the app. Distinct from any other delivery failure (a
/// timeout, a 5xx from the push service, ...) so <see cref="PushNotificationDispatcher"/> only ever
/// prunes a subscription it knows will never work again.
/// </summary>
public sealed class PushSubscriptionExpiredException : Exception
{
    public PushSubscriptionExpiredException(string message) : base(message)
    {
    }
}
