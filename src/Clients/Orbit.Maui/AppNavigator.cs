using Orbit.Maui.Features.Account;
using Orbit.Maui.Features.Authentication;
using Orbit.Maui.Features.Notes;
using Orbit.Mobile.Authentication;

namespace Orbit.Maui;

/// <summary>
/// Moves the app between its three top-level screens by replacing the window's page outright.
///
/// Deliberately not Shell navigation: these are destinations that replace each other, never a stack.
/// Signing in must not leave the sign-in screen behind a back gesture, and - the reason this matters -
/// a build the server has retired must have no navigation stack at all to be swiped past. A blocked app
/// simply *is* the startup screen.
/// </summary>
public sealed class AppNavigator
{
	private readonly IServiceProvider _services;

	public AppNavigator(IServiceProvider services, SessionStore sessionStore)
	{
		_services = services;

		// A session can end far from any screen - a refresh token the server has revoked takes it away
		// from inside an HTTP call. Watching the store here means every such path lands on the sign-in
		// screen, instead of each screen having to notice for itself and one of them forgetting.
		sessionStore.Changed += session =>
		{
			if (session is null)
			{
				ShowSignIn();
			}
		};
	}

	public void ShowSignIn() => ShowAsRoot<SignInPage>();

	public void ShowRegister() => ShowAsRoot<RegisterPage>();

	public void ShowAccount() => ShowAsRoot<AccountPage>();

	public void ShowNotes() => ShowAsRoot<NotesPage>();

	private void ShowAsRoot<TPage>() where TPage : Page
		=> MainThread.BeginInvokeOnMainThread(() =>
		{
			if (Application.Current?.Windows.FirstOrDefault() is { } window)
			{
				window.Page = _services.GetRequiredService<TPage>();
			}
		});
}
