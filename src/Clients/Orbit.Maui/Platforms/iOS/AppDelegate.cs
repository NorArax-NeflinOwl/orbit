using Foundation;
using Orbit.Maui.Platform;
using Orbit.Mobile.Notifications;
using UIKit;
using UserNotifications;

namespace Orbit.Maui;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	/// <summary>Held because UNUserNotificationCenter keeps its delegate weakly - a local would be collected and taps would quietly stop arriving.</summary>
	private NotificationTapListener? _tapListener;

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	/// <summary>
	/// The tap listener has to be installed before iOS delivers the response that launched the app, and
	/// this is the first point where both the app's services and UIKit exist.
	/// </summary>
	public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
	{
		var launched = base.FinishedLaunching(application, launchOptions);

		var services = IPlatformApplication.Current!.Services;
		_tapListener = new NotificationTapListener(services.GetRequiredService<PendingNotificationTap>());
		UNUserNotificationCenter.Current.Delegate = _tapListener;

		return launched;
	}
}
