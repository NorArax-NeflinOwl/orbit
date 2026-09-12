namespace Orbit.Maui.Controls;

/// <summary>
/// Whether the phone has been asked to animate at all - the platform's answer to the prefers-reduced-motion
/// media query Orbit.Web's app.css writes its rules under. MAUI does not expose the setting, so it is read
/// from the platform: Android says so by scaling every animation to nothing (the "Remove animations"
/// switch in Accessibility, and the animator duration scale in Developer options, both set it to 0). Read
/// each time it is asked, since it can change while the app is open.
///
/// Every other head answers yes. iOS has the same setting (UIAccessibility.IsReduceMotionEnabled), but no
/// iOS head is built on the machine this is written on, so it is not read yet rather than read untested.
/// </summary>
internal static class Motion
{
	public static bool IsWanted =>
#if ANDROID
		Android.Provider.Settings.Global.GetFloat(
			Android.App.Application.Context.ContentResolver,
			Android.Provider.Settings.Global.AnimatorDurationScale,
			1f) > 0f;
#else
		true;
#endif
}
