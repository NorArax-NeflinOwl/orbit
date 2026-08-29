namespace Orbit.Maui.Platform;

/// <summary>
/// Always, on iOS. The map is MapKit, which is part of the platform and needs no key and no account -
/// see the Android counterpart, where it is neither.
/// </summary>
public static class MapAvailability
{
	public static bool CanShowMap => true;
}
