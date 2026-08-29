using Orbit.Core.Mobile;

namespace Orbit.Mobile.Update;

/// <summary>
/// What the app reports about itself when it asks the server whether it may run. Orbit.Maui supplies
/// this from MAUI's <c>AppInfo</c>, so the version can never drift from the one the build stamped, and
/// the platform is whichever head is running.
/// </summary>
public sealed record AppVersion(MobilePlatform Platform, string DisplayVersion);
