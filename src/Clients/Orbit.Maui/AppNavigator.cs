using Orbit.Maui.Features.Notes;
using Orbit.Maui.Features.Authentication;

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

	public AppNavigator(IServiceProvider services) => _services = services;

	public void ShowSignIn() => ShowAsRoot<SignInPage>();

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
