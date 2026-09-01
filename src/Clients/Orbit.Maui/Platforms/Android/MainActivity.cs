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
// A public share link, opened in Orbit rather than in a browser. The host is the deployment's own and
// is fixed at build time - see OrbitShareLinkHost in Orbit.Maui.csproj - because an intent filter is an
// attribute, and a filter with no host would offer Orbit for every link on the phone.
//
// AutoVerify asks Android to check https://<host>/.well-known/assetlinks.json for this app's package
// and signing certificate. Without that file Android 12 and later will not route the link on its own:
// the reader has to allow it under Settings > Apps > Orbit > Open by default. See
// info/functionality.md's "A link opened on a phone" for what a deployment has to serve.
[IntentFilter(
	[Intent.ActionView],
	Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
	DataScheme = "https",
	DataHost = OrbitShareLinks.Host,
	DataPathPrefix = "/s/",
	AutoVerify = true)]
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
		MatchStatusBarToTheme();

		// Choosing Orbit's own theme changes no configuration, so OnConfigurationChanged never runs for
		// it and the bar would keep the theme the app started in.
		if (Microsoft.Maui.Controls.Application.Current is { } application)
		{
			application.RequestedThemeChanged += (_, _) => MatchStatusBarToTheme();
		}
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
	///
	/// This is only half of it: the reader can also change Orbit's theme without the system's changing at
	/// all, which reaches no configuration change - see the subscription in OnCreate.
	/// </summary>
	public override void OnConfigurationChanged(Android.Content.Res.Configuration newConfig)
	{
		base.OnConfigurationChanged(newConfig);
		MatchStatusBarToTheme();
	}

	/// <summary>
	/// Dark icons over the light status bar and light ones over the dark, and below API 35 the bar's
	/// colour as well.
	///
	/// Android does not work out the icon contrast from the bar's colour. Left alone, the white icons it
	/// defaults to sit on Orbit's white bar and simply are not there: no clock, no battery, no signal.
	///
	/// The colour is set here rather than left to colorPrimaryDark and its values-night counterpart,
	/// which was enough only while the app followed the system. Orbit now has a theme of its own - see
	/// AccountPage's appearance section - and Android resolves that resource by the *system's* night
	/// mode, so a phone in dark mode showing a reader who chose Light kept a black bar above a white
	/// app, with the clock unreadable on it. Read from the app's own theme, the two cannot disagree.
	/// From API 35 the app draws behind the bar and nothing paints it at all, which is why that half is
	/// skipped there.
	/// </summary>
	private void MatchStatusBarToTheme()
	{
		if (Window is not { DecorView: { } decorView } window)
		{
			return;
		}

		var isDark = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark;
		WindowCompat.GetInsetsController(window, decorView).AppearanceLightStatusBars = !isDark;

		if (!OperatingSystem.IsAndroidVersionAtLeast(35))
		{
			// The two surface colours from Resources/Styles/Colors.xaml, which the values/colors.xml pair
			// mirrors - see the comments there.
			window.SetStatusBarColor(isDark ? Android.Graphics.Color.ParseColor("#1C1C1E") : Android.Graphics.Color.White);
		}
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
		if (DestinationOf(intent) is not { Length: > 0 } url)
		{
			return;
		}

		IPlatformApplication.Current?.Services.GetService<PendingNotificationTap>()?.Record(url);
	}

	/// <summary>
	/// Where this intent wants to go, and taken off it so it is followed once - see the note on
	/// <see cref="RecordNotificationTap"/>.
	///
	/// Two ways in, and they carry it differently. A tapped notification has it as a string extra put
	/// there by Firebase; a link Android handed to Orbit instead of the browser has it as the intent's
	/// own data. Only the path is kept from the link: the destinations are read as paths, and the host
	/// is Orbit's own or the filter would not have matched - see NotificationDestination.
	/// </summary>
	private static string? DestinationOf(Intent? intent)
	{
		if (intent?.GetStringExtra(UrlKey) is { Length: > 0 } url)
		{
			intent.RemoveExtra(UrlKey);
			return url;
		}

		if (intent?.Data is { Path: { Length: > 0 } path })
		{
			// SetData rather than the read-only Data property, which the binding does not let you assign.
			intent.SetData(null);
			return path;
		}

		return null;
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
