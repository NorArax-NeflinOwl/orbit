using Orbit.Mobile.Diagnostics;

namespace Orbit.Maui.Platform;

/// <summary>Answers from MAUI's DeviceInfo, so a log says what it actually ran on.</summary>
public sealed class PhoneDescription : IDeviceDescription
{
	public string OperatingSystemVersion => DeviceInfo.Current.VersionString;

	public string Model => DeviceInfo.Current.Model;
}
