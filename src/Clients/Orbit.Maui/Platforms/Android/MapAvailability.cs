using Android.Content.PM;
using AndroidApplication = Android.App.Application;

namespace Orbit.Maui.Platform;

/// <summary>
/// Whether this build can show a map, which on Android is a question about the build rather than the
/// device: Google Maps reads its key from the manifest, and creating the map view without one throws
/// from inside Play Services and takes the whole app down. The map is one tap from every screen, so
/// that is a crash the first curious reader finds.
///
/// The key is merged in from AndroidManifestOverlay.xml, which is gitignored - see the .example beside
/// it. A build made without it is an ordinary state here rather than a fault, and the map screen says
/// so instead of the process disappearing.
/// </summary>
public static class MapAvailability
{
	/// <summary>The manifest entry Google Maps reads its key from. Its name is fixed by Play Services.</summary>
	private const string ApiKeyName = "com.google.android.geo.API_KEY";

	/// <summary>Asked once: the manifest cannot change while the app is running.</summary>
	private static readonly Lazy<bool> HasApiKey = new(ReadApiKey);

	public static bool CanShowMap => HasApiKey.Value;

	private static bool ReadApiKey()
	{
		var context = AndroidApplication.Context;
		var application = context.PackageManager?.GetApplicationInfo(
			context.PackageName!, PackageInfoFlags.MetaData);

		return !string.IsNullOrWhiteSpace(application?.MetaData?.GetString(ApiKeyName));
	}
}
