using Orbit.Mobile.Update;

namespace Orbit.Mobile.Diagnostics;

/// <summary>
/// What a log came from. Attached to an upload so a report can be read against the build and the
/// hardware that produced it - the plan's §8 names these four, and they are the difference between "it
/// crashed" and "it crashes on that iOS version".
///
/// <see cref="AppVersion"/> already carries the version and the platform, so this only adds what it
/// does not: the operating system and the model. Behind an interface for the usual reason - both are
/// platform calls.
/// </summary>
public interface IDeviceDescription
{
    string OperatingSystemVersion { get; }

    string Model { get; }
}
