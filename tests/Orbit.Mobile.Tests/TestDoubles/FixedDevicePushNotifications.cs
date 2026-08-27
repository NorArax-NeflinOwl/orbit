using Orbit.Mobile.Notifications;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// A device that answers however the test needs it to. Stands in for the one thing no test can do -
/// grant a notification permission - the same way FixedDeviceLocation does for a position.
/// </summary>
internal sealed class FixedDevicePushNotifications : IDevicePushNotifications
{
    public string Platform => "Ios";

    public PushRegistrationResult Result { get; set; } =
        new(PushRegistrationOutcome.Registered, "token-from-a-test");

    /// <summary>Set to make the platform call throw, as a build without the right entitlement would.</summary>
    public Exception? Failure { get; set; }

    public Task<PushRegistrationResult> RegisterAsync(CancellationToken cancellationToken = default)
        => Failure is { } failure ? Task.FromException<PushRegistrationResult>(failure) : Task.FromResult(Result);
}
