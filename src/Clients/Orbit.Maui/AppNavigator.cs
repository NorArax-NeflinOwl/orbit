using Orbit.Maui.Features.Account;
using Orbit.Maui.Features.Authentication;
using Orbit.Maui.Features.Calendar;
using Orbit.Maui.Features.Copies;
using Orbit.Maui.Features.Chat;
using Orbit.Maui.Features.Dashboard;
using Orbit.Maui.Features.Diagnostics;
using Orbit.Maui.Features.Inventory;
using Orbit.Maui.Features.Location;
using Orbit.Maui.Features.Notes;
using Orbit.Maui.Features.Notifications;
using Orbit.Maui.Features.Sharing;
using Orbit.Maui.Features.Tasks;
using Orbit.Maui.Features.Update;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Data;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Navigation;

namespace Orbit.Maui;

/// <summary>
/// Moves the app between its three top-level screens by replacing the window's page outright. The one
/// implementation of <see cref="IScreenNavigator"/> - which exists so the view models, which need only
/// this, do not need a MAUI project to be tested in.
///
/// Deliberately not Shell navigation: these are destinations that replace each other, never a stack.
/// Signing in must not leave the sign-in screen behind a back gesture, and - the reason this matters -
/// a build the server has retired must have no navigation stack at all to be swiped past. A blocked app
/// simply *is* the startup screen.
/// </summary>
public sealed class AppNavigator : IScreenNavigator
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

	public void ShowSignIn() => ShowAsRoot<SignInPage>(Screen.SignIn);

	public void ShowRegister() => ShowAsRoot<RegisterPage>(Screen.Register);

	public void ShowPasswordReset() => ShowAsRoot<PasswordResetPage>(Screen.PasswordReset);

	public void ShowAccount() => ShowAsRoot<AccountPage>(Screen.Account);

	public void ShowChatKeyGate() => ShowAsRoot<ChatKeyGatePage>(Screen.ChatKeyGate);

	public void ShowContacts() => ShowAsRoot<ContactsPage>(Screen.Contacts);

	public void ShowTasks() => ShowAsRoot<TasksPage>(Screen.Tasks);

	public void ShowCalendar() => ShowAsRoot<CalendarPage>(Screen.Calendar);

	public void ShowInventory() => ShowAsRoot<InventoryPage>(Screen.Inventory);

	public void ShowMap() => ShowAsRoot<MapPage>(Screen.Map);

	public void ShowWarehouse(Guid localId)
		=> ShowAsRoot<WarehouseDetailPage>(Screen.Warehouse, page => page.ViewModel.Open(localId));

	public void ShowNotifications() => ShowAsRoot<NotificationFeedPage>(Screen.Notifications);

	public void ShowSharedLink(string token)
		=> ShowAsRoot<SharedLinkPage>(Screen.SharedLink, page => page.ViewModel.Open(token));

	public void ShowUpdate() => ShowAsRoot<UpdatePage>(Screen.Update);

	// No ShowNotificationSettings any more: the settings moved onto the account screen - see
	// AccountPage's notification section - so there is no page of their own left to navigate to.
	public void ShowDiagnostics() => ShowAsRoot<DiagnosticsPage>(Screen.Diagnostics);

	public void ShowTaskList(Guid localId)
		=> ShowAsRoot<TaskListDetailPage>(Screen.TaskList, page => page.ViewModel.Open(localId));

	public void ShowTaskItem(Guid taskListLocalId, Guid itemId)
		=> ShowAsRoot<TaskItemSummaryPage>(Screen.TaskItem, page => page.ViewModel.Open(taskListLocalId, itemId));

	public void ShowNote(Guid localId)
		=> ShowAsRoot<NoteDetailPage>(Screen.Note, page => page.ViewModel.Open(localId));

	public void ShowCopyReview() => ShowAsRoot<CopyReviewPage>(Screen.CopyReview);

	public void ShowCopyHistory(CopyKind kind, Guid localId)
		=> ShowAsRoot<CopyHistoryPage>(Screen.CopyHistory, page => page.ViewModel.Open(kind, localId));

	public void ShowCalendarEvent(Guid localId)
		=> ShowAsRoot<CalendarEventDetailPage>(Screen.CalendarEvent, page => page.ViewModel.Open(localId));

	/// <summary>
	/// A conversation needs to know whose it is, and these screens are resolved from the container rather
	/// than constructed - so the page is told after it exists, before it is shown.
	/// </summary>
	public void ShowConversation(LocalContact contact)
		=> ShowAsRoot<ConversationPage>(Screen.Conversation, page => page.ViewModel.Open(contact));

	public void ShowGroups() => ShowAsRoot<GroupsPage>(Screen.Groups);

	/// <inheritdoc cref="ShowConversation"/>
	public void ShowGroupConversation(LocalChatGroup group)
		=> ShowAsRoot<GroupConversationPage>(Screen.GroupConversation, page => page.ViewModel.Open(group));

	/// <inheritdoc cref="ShowConversation"/>
	public void ShowGroupDetail(LocalChatGroup group)
		=> ShowAsRoot<GroupDetailPage>(Screen.GroupDetail, page => page.ViewModel.Open(group));

	public void ShowDashboard() => ShowAsRoot<DashboardPage>(Screen.Dashboard);

	public void ShowNotes() => ShowAsRoot<NotesPage>(Screen.Notes);

	private void ShowAsRoot<TPage>(Screen screen, Action<TPage>? prepare = null) where TPage : Page
		=> MainThread.BeginInvokeOnMainThread(() =>
		{
			if (Application.Current?.Windows.FirstOrDefault() is not { } window)
			{
				return;
			}

			var page = _services.GetRequiredService<TPage>();
			prepare?.Invoke(page);
			window.Page = page;

			// Resolved here rather than taken in the constructor because UpNavigation needs this class:
			// asking for it up front is a cycle the container cannot build. Everything else this method
			// uses is resolved the same way, so it is the shape this class already has.
			_services.GetRequiredService<UpNavigation>().Showing(screen);

			// The navigation bar is one instance shared by every page, so a menu opened over the screen
			// being left would still be open over the one arriving. Orbit.Web closes it on every route
			// change rather than in each thing that navigates - see MainLayout's LocationChanged - and
			// this is the one place mobile changes screens.
			_services.GetRequiredService<NavigationBarViewModel>().CloseMenu();
		});
}
