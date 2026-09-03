namespace Orbit.Mobile.Security;

/// <summary>
/// Whether private notes, lists and inventories are readable on this phone right now.
///
/// Orbit's <c>IsPrivate</c> already means "only the owner can read this, and the server never can". On a
/// phone the missing half of that is physical: a private note on an unlocked handset is readable by
/// whoever is holding it. This is the counterpart the plan's §9 asks for - the same promise, against the
/// person rather than against the server.
///
/// Unlocked for a while rather than per item. Being asked for a face once per note would make the
/// feature something people turn off, and the thing being protected is the phone leaving its owner's
/// hands - which locking on the way to the background covers.
/// </summary>
public sealed class PrivateItemGate
{
    private readonly IDeviceAuthentication _deviceAuthentication;

    public PrivateItemGate(IDeviceAuthentication deviceAuthentication)
        => _deviceAuthentication = deviceAuthentication;

    /// <summary>Raised when private things become readable or stop being, so a list can redraw.</summary>
    public event EventHandler? Changed;

    public bool IsUnlocked { get; private set; }

    /// <summary>
    /// True once private things may be shown. Asks only when they are not already showing, so tapping a
    /// second private note after unlocking does not prompt again.
    /// </summary>
    public async Task<bool> TryUnlockAsync(CancellationToken cancellationToken = default)
    {
        if (IsUnlocked)
        {
            return true;
        }

        var outcome = await _deviceAuthentication.ConfirmAsync(
            "Unlock your private items", cancellationToken);

        // A device with nothing to ask with stays locked. Letting somebody in because the phone has no
        // passcode would be exactly backwards: that is the phone least likely to still be in the hands
        // of its owner.
        if (outcome != DeviceAuthenticationOutcome.Confirmed)
        {
            return false;
        }

        IsUnlocked = true;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Called when the app leaves the foreground. The moment somebody puts the phone down is the moment
    /// the thing being guarded against becomes possible.
    /// </summary>
    public void Lock()
    {
        if (!IsUnlocked)
        {
            return;
        }

        IsUnlocked = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
