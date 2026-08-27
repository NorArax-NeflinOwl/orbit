using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Activity;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Mobile.Screens.Navigation;

namespace Orbit.Maui;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		OnBackPressedDispatcher.AddCallback(this, new GoUpOnBack(this));
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
