using Orbit.Mobile.Diagnostics;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>A device that is always the same one, so an upload's contents can be asserted on.</summary>
internal sealed class FixedDeviceDescription : IDeviceDescription
{
    public string OperatingSystemVersion => "18.0";

    public string Model => "iPhone17,1";
}
