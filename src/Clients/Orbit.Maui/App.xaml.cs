using Orbit.Mobile.Authentication;
using Orbit.Mobile.Notifications;
using Orbit.Mobile.Live;
using Orbit.Mobile.Presence;
using Orbit.Mobile.Screens.Account;
using Orbit.Mobile.Security;

namespace Orbit.Maui;

public partial class App : Application
{
	private readonly IServiceProvider _services;

	public App(IServiceProvider services)
	{
		InitializeComponent();
		_services = services;

		// Tapping a notification while Orbit is already running resumes it rather than launching it, so
		// the startup flow - which follows the tap on a cold start - never runs. Subscribing to the tap
		// itself rather than to the window's Resumed event is deliberate: iOS resumes the app before it
		// delivers the tap, so Resumed fires while there is still nothing to follow.
		_services.GetRequiredService<PendingNotificationTap>().RecordedWhileRunning +=
			(_, url) => _ = FollowAsync(url);

		// The theme the reader chose, put back the moment the app starts. Only the account screen used to
		// apply it, so the choice lasted until the app was closed and then came back as the phone's - a
		// setting that forgets itself is worse than none at all.
		ApplyTheme(_services.GetRequiredService<IThemeStore>().Read());

		// The accent depends on the theme as well as on the hue, so it is re-applied whenever the theme
		// changes - including when the phone itself switches at dusk, which the app is only told about.
		RequestedThemeChanged += (_, _) => ApplyStoredAccent();
		ApplyStoredAccent();
	}

	/// <summary>
	/// Paints the four accent colours from the hue the reader picked. Everything that draws with them
	/// asks by DynamicResource, so this reaches a screen already on display rather than the next one.
	///
	/// Static like <see cref="ApplyTheme"/>, and for the same reason: it is needed before any screen
	/// exists. <see cref="AccentPalette"/> works the colours out; this only says where they go.
	/// </summary>
	public static void ApplyAccent(AccentColor accentColor)
	{
		if (Current is not { } application)
		{
			return;
		}

		var palette = AccentPalette.For(accentColor.Hue, application.RequestedTheme == AppTheme.Dark);
		application.Resources["Accent"] = Color.FromArgb(palette.Accent);
		application.Resources["AccentHover"] = Color.FromArgb(palette.AccentHover);
		application.Resources["AccentSubtle"] = Color.FromArgb(palette.AccentSubtle);
		application.Resources["AccentOn"] = Color.FromArgb(palette.AccentOn);

		// MAUI's own control styles reach for Primary by name, so it follows the accent rather than
		// staying the purple this app started life as.
		application.Resources["Primary"] = Color.FromArgb(palette.Accent);
	}

	private void ApplyStoredAccent() => ApplyAccent(_services.GetRequiredService<IAccentColorStore>().Read());

	/// <summary>
	/// Turns the reader's choice into the app's theme. Unspecified means "follow the phone", which is
	/// what Orbit did before there was a choice at all.
	///
	/// Static and here rather than on the account screen, because it is needed before any screen exists.
	/// </summary>
	public static void ApplyTheme(ChosenTheme theme)
	{
		if (Current is { } application)
		{
			application.UserAppTheme = theme switch
			{
				ChosenTheme.Light => AppTheme.Light,
				ChosenTheme.Dark => AppTheme.Dark,
				_ => AppTheme.Unspecified
			};
		}
	}

	/// <summary>
	/// Startup always begins at the version gate - nothing else may run before the app knows it is still
	/// allowed to. See <see cref="Features.Startup.StartupViewModel"/>.
	/// </summary>
	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(_services.GetRequiredService<Features.Startup.StartupPage>());

		// Private things stop being readable the moment the app is put down, which is the moment the
		// phone can change hands - see PrivateItemGate.
		window.Deactivated += (_, _) =>
		{
			_services.GetRequiredService<PrivateItemGate>().Lock();

			// And this account stops being shown as present. Going quiet is how that works - the server
			// ages a silent account out on its own - so stopping is the whole mechanism.
			_services.GetRequiredService<PresenceReporter>().Stop();

			// The live connection goes with it. A socket held open behind a locked screen is one Android
			// drops in Doze anyway, and what it would have carried is what push already delivers - see
			// LiveUpdatesConnection.
			_ = _services.GetRequiredService<LiveUpdatesConnection>().StopAsync();
		};

		window.Activated += (_, _) =>
		{
			_services.GetRequiredService<PresenceReporter>().Start();
			_ = _services.GetRequiredService<LiveUpdatesConnection>().StartAsync();
		};

		return window;
	}

	/// <summary>
	/// Deliberately checks for a session first. A tap can arrive after the reader has signed out, and
	/// following it would put a conversation behind the sign-in screen.
	/// </summary>
	private async Task FollowAsync(string url)
	{
		if (await _services.GetRequiredService<SessionStore>().GetAsync() is null)
		{
			return;
		}

		await _services.GetRequiredService<NotificationOpener>().OpenAsync(url);
	}
}
