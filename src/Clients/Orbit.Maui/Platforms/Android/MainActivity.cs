using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using AndroidX.Activity;
using AndroidX.Core.View;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Mobile.Notifications;
using Orbit.Mobile.Screens.Navigation;

namespace Orbit.Maui;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	/// <summary>
	/// The extra carrying the destination. Android's Firebase SDK turns every entry of a message's
	/// "data" block into a string extra of this name on the launching intent, and Orbit.Api's
	/// FirebasePushNotificationSender puts the tap target there under "url". Changing it on either side
	/// breaks tap-through silently, which is the same warning the iOS listener carries about its own
	/// half - the two platforms read the destination from different places in the same message.
	/// </summary>
	private const string UrlKey = "url";

	protected override void OnCreate(Bundle? savedInstanceState)
	{
		// Before base.OnCreate, which is what builds the window and puts the startup screen on it. The
		// startup flow takes the destination as it decides where to open, so a tap written down after
		// that is a tap nobody ever follows. This is the cold-start half; OnNewIntent is the other.
		RecordNotificationTap(Intent);

		base.OnCreate(savedInstanceState);
		OnBackPressedDispatcher.AddCallback(this, new GoUpOnBack(this));
		KeepContentInsideTheSystemBars();
		MatchStatusBarIconsToTheme();
	}

	/// <summary>
	/// Android only forces an app to draw behind the system bars from API 35 on. MAUI turns the decor
	/// fitting off on every version regardless, and then applies the insets back only from 35 up - so on
	/// 29 to 34 the window is edge to edge with nothing accounting for it, and the navigation bar's
	/// avatar lands under the status bar while the sync strip is squeezed to nothing behind the
	/// three-button bar.
	///
	/// Below 35 Orbit wants the older arrangement anyway: the status bar painted by the system with
	/// colorPrimaryDark (see Resources/values/colors.xml) rather than by whatever happens to be at the
	/// top of the page.
	/// </summary>
	private void KeepContentInsideTheSystemBars()
	{
		if (OperatingSystem.IsAndroidVersionAtLeast(35) || Window is null)
		{
			return;
		}

		WindowCompat.SetDecorFitsSystemWindows(Window, true);
	}

	/// <summary>
	/// UiMode is in this activity's ConfigurationChanges, so switching the system between light and dark
	/// does not recreate it - which means nothing would revisit the status bar and its icons would stay
	/// the previous mode's colour.
	/// </summary>
	public override void OnConfigurationChanged(Android.Content.Res.Configuration newConfig)
	{
		base.OnConfigurationChanged(newConfig);
		MatchStatusBarIconsToTheme();
	}

	/// <summary>
	/// Dark icons over the light status bar and light ones over the dark.
	///
	/// The bar's colour comes from the theme - colorPrimaryDark, and its values-night counterpart - but
	/// Android does not work out the contrast from it. Left alone, the white icons it defaults to sit on
	/// Orbit's white bar and simply are not there: no clock, no battery, no signal.
	/// </summary>
	private void MatchStatusBarIconsToTheme()
	{
		if (Window is not { DecorView: { } decorView } window)
		{
			return;
		}

		var isDark = (Resources?.Configuration?.UiMode & UiMode.NightMask) == UiMode.NightYes;
		WindowCompat.GetInsetsController(window, decorView).AppearanceLightStatusBars = !isDark;
	}

	/// <summary>
	/// A tap arriving while Orbit is already running, which reaches the activity that already exists
	/// rather than starting a second one - that is what SingleTop above buys, and without it this
	/// method is never called at all.
	/// </summary>
	protected override void OnNewIntent(Intent? intent)
	{
		base.OnNewIntent(intent);

		// So that anything asking the activity what started it sees the newest answer rather than the
		// one it launched with.
		Intent = intent;
		RecordNotificationTap(intent);
	}

	/// <summary>
	/// Writes the destination down and takes it off the intent, so it is followed once. Android hands
	/// the same intent back when it recreates an activity it had to destroy, and a destination left on
	/// it would send the reader to the same conversation every time that happened.
	///
	/// Only written down here, never followed: this runs before there is a signed-in session or a
	/// screen to replace, which is the whole reason PendingNotificationTap exists.
	/// </summary>
	private static void RecordNotificationTap(Intent? intent)
	{
		if (intent?.GetStringExtra(UrlKey) is not { Length: > 0 } url)
		{
			return;
		}

		intent.RemoveExtra(UrlKey);
		IPlatformApplication.Current?.Services.GetService<PendingNotificationTap>()?.Record(url);
	}

	/// <summary>
	/// Answers the phone's back gesture with the screen hierarchy, because there is no stack to pop -
	/// see <see cref="UpNavigation"/>. Without this, back leaves the app from every screen, including
	/// the ones a reader opened from a list and expects to come back out of.
	///
	/// Through OnBackPressedDispatcher rather than by overriding OnBackPressed, which Android deprecated
	/// in favour of it, and which the predictive back gesture does not call at all.
	/// </summary>
	private sealed class GoUpOnBack : OnBackPressedCallback
	{
		private readonly MainActivity _activity;

		public GoUpOnBack(MainActivity activity) : base(true) => _activity = activity;

		public override void HandleOnBackPressed()
		{
			if (IPlatformApplication.Current?.Services.GetService<UpNavigation>()?.GoUp() == true)
			{
				return;
			}

			// Nothing above this screen. Sent to the background rather than finished, which is what
			// Android itself does from a launcher activity - the app comes back where it was left,
			// instead of starting cold and losing whatever was half-typed.
			_activity.MoveTaskToBack(true);
		}
	}
}
