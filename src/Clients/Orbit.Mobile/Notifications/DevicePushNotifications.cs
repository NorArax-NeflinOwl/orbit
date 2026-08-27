namespace Orbit.Mobile.Notifications;

/// <summary>How the device answered when asked to receive push notifications.</summary>
public enum PushRegistrationOutcome
{
    Registered,

    /// <summary>The reader declined, or notifications are switched off for the app in Settings.</summary>
    NotPermitted,

    /// <summary>
    /// Permitted, but this build cannot produce a token to register. Distinct from a refusal because
    /// nothing the reader does will fix it - see <see cref="IDevicePushNotifications"/>.
    /// </summary>
    NotAvailableHere
}

/// <param name="DeviceToken">The token to register with Orbit, present only when the outcome is Registered.</param>
public sealed record PushRegistrationResult(PushRegistrationOutcome Outcome, string? DeviceToken = null);

/// <summary>
/// Asking the device for permission to notify, and for the token that lets Orbit reach it.
///
/// Behind an interface for the same reason as IDeviceLocation: it is a platform call, and it is the one
/// part of this feature that asks the reader for something no test can grant.
///
/// The token is where the two platforms diverge. Orbit's server sends through Firebase Cloud Messaging,
/// which reaches Android directly and iOS through APNs underneath, so what the server wants is an FCM
/// registration token rather than a raw APNs one. Producing that needs the Firebase SDK in the app, and
/// - on iOS - an APNs auth key uploaded to the Firebase console before anything actually arrives. Until
/// that key exists the honest answer is <see cref="PushRegistrationOutcome.NotAvailableHere"/>: a
/// registration nothing can deliver to is worse than none, because the server would count the device as
/// reachable and stop trying anything else.
/// </summary>
public interface IDevicePushNotifications
{
    /// <summary>
    /// Which platform this device is, in the spelling the server's MobilePlatform expects. Read from the
    /// implementation rather than guessed at the call site, so the one place that knows says so.
    /// </summary>
    string Platform { get; }

    /// <summary>
    /// Asks the reader for permission if it has not been asked, then obtains a token. Safe to call on
    /// every sign-in: the OS only prompts once, and a token can change, so repeating is how it stays
    /// current.
    /// </summary>
    Task<PushRegistrationResult> RegisterAsync(CancellationToken cancellationToken = default);
}
