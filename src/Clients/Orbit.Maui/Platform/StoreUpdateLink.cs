using Orbit.Mobile.Update;

namespace Orbit.Maui.Platform;

/// <summary>
/// Opens the store listing the server named, using whatever the platform considers the right app for
/// it - the App Store on iOS, Play on Android.
/// </summary>
public sealed class StoreUpdateLink : IUpdateLink
{
	public Task OpenAsync(string url) => Launcher.Default.OpenAsync(url);
}
