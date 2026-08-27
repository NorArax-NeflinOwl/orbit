using Orbit.Mobile.Authentication;
using Orbit.Mobile.Notifications;

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
	}

	/// <summary>
	/// Startup always begins at the version gate - nothing else may run before the app knows it is still
	/// allowed to. See <see cref="Features.Startup.StartupViewModel"/>.
	/// </summary>
	protected override Window CreateWindow(IActivationState? activationState)
		=> new(_services.GetRequiredService<Features.Startup.StartupPage>());

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
