using Orbit.Maui.Features.Account;
using Orbit.Maui.Features.Authentication;
using Orbit.Maui.Features.Calendar;
using Orbit.Maui.Features.Chat;
using Orbit.Maui.Features.Notes;
using Orbit.Maui.Features.Tasks;
using Orbit.Mobile.Data;
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

	public void ShowChatKeyGate() => ShowAsRoot<ChatKeyGatePage>();

	public void ShowContacts() => ShowAsRoot<ContactsPage>();

	public void ShowTasks() => ShowAsRoot<TasksPage>();

	public void ShowCalendar() => ShowAsRoot<CalendarPage>();

	public void ShowTaskList(Guid localId)
		=> ShowAsRoot<TaskListDetailPage>(page => ((TaskListDetailViewModel)page.BindingContext).Open(localId));

	/// <summary>
	/// A conversation needs to know whose it is, and these screens are resolved from the container rather
	/// than constructed - so the page is told after it exists, before it is shown.
	/// </summary>
	public void ShowConversation(LocalContact contact)
		=> ShowAsRoot<ConversationPage>(page => ((ConversationViewModel)page.BindingContext).Open(contact));

	public void ShowNotes() => ShowAsRoot<NotesPage>();

	private void ShowAsRoot<TPage>(Action<TPage>? prepare = null) where TPage : Page
		=> MainThread.BeginInvokeOnMainThread(() =>
		{
			if (Application.Current?.Windows.FirstOrDefault() is not { } window)
			{
				return;
			}

			var page = _services.GetRequiredService<TPage>();
			prepare?.Invoke(page);
			window.Page = page;
		});
}
