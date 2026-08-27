namespace Orbit.Mobile.Security;

/// <summary>How the device answered when asked to confirm who is holding it.</summary>
public enum DeviceAuthenticationOutcome
{
    Confirmed,

    /// <summary>The reader cancelled, or the face or finger did not match.</summary>
    Refused,

    /// <summary>
    /// Nothing to ask with: no Face ID, no Touch ID, no passcode. Distinct from a refusal because the
    /// reader cannot fix it by trying again - see <see cref="PrivateItemGate"/> for what is done about
    /// it, which is deliberately not "let them in anyway".
    /// </summary>
    NotAvailableOnThisDevice
}

/// <summary>
/// Asking the device to confirm that the person holding it is the person who owns it.
///
/// Behind an interface for the same reason as the other platform seams, and one more: no test can pass
/// a face, so the alternative to this is a feature nothing can check.
/// </summary>
public interface IDeviceAuthentication
{
    /// <param name="reason">
    /// Shown by the system in its own prompt, so it has to say what is about to be revealed rather than
    /// what the app is doing - "Unlock your private notes", not "Authenticate".
    /// </param>
    Task<DeviceAuthenticationOutcome> ConfirmAsync(string reason, CancellationToken cancellationToken = default);
}
