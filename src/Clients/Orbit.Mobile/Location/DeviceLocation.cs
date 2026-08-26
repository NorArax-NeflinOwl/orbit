namespace Orbit.Mobile.Location;

/// <summary>Where the phone currently is, and what the reader had to agree to for it to say.</summary>
public enum DeviceLocationOutcome
{
    Found,

    /// <summary>The reader declined location access, or it is switched off for the whole device.</summary>
    NotPermitted,

    /// <summary>Permitted, but nothing came back - indoors, airplane mode, a simulator with no location set.</summary>
    Unavailable
}

/// <param name="Address">
/// Best-effort reverse geocoding, and often null - it needs a network round trip and is not worth
/// failing a position over. The coordinates are the part that matters.
/// </param>
public sealed record DeviceLocationResult(
    DeviceLocationOutcome Outcome, double Latitude = 0, double Longitude = 0, string? Address = null);

/// <summary>
/// Reading the device's own position. Behind an interface for the usual reason - it is a platform call,
/// and it is the one thing about locations that cannot be decided without a device - but also because
/// it is the only part of this feature that asks the reader for permission, which no test can grant.
/// </summary>
public interface IDeviceLocation
{
    Task<DeviceLocationResult> ReadAsync(CancellationToken cancellationToken = default);
}
