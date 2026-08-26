namespace Orbit.Core.Diagnostics;

/// <summary>
/// What the phone that produced a log was, recorded alongside every entry from one upload. Without it a
/// report says what went wrong but not where, and "only on Android 13" or "only on the old build" is
/// usually the half that identifies the bug.
///
/// These travel together and never change once an upload is stored, hence one value object rather than
/// four columns' worth of loose parameters threaded through the repository.
/// </summary>
public sealed record MobileDeviceInfo(string AppVersion, string Platform, string OperatingSystemVersion, string DeviceModel)
{
    /// <summary>
    /// Trims each field to what the schema holds, so an oversized value from a client that got something
    /// wrong is truncated rather than failing the whole upload - the entries are still worth keeping.
    /// </summary>
    public MobileDeviceInfo Truncated() => new(
        Limit(AppVersion, 40),
        Limit(Platform, 20),
        Limit(OperatingSystemVersion, 40),
        Limit(DeviceModel, 80));

    private static string Limit(string value, int maximumLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length <= maximumLength ? trimmed : trimmed[..maximumLength];
    }
}
