using Orbit.Maui.Features.About;
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
using Orbit.Maui.Features.Places;
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
/// Moves the app between screens by replacing the window's page outright. The one implementation of
/// <see cref="IScreenNavigator"/> - which exists so the view models, which need only this, do not need
/// a MAUI project to be tested in.
///
/// Still not Shell or NavigationPage navigation: one page is on screen at a time and Orbit draws its
/// own bar over it, so a second set of platform chrome would have to be fought rather than used. What
/// there now *is* is a history - see <see cref="ScreenHistory"/>, which every method below tells how it
/// arrived. Signing in still leaves nothing behind it, because that is a root arrival and a root
/// clears; and a build the server has retired still has nothing at all to be swiped past, because a
/// blocked app never leaves the startup screen and so never joins the history.
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

	public void ShowSignIn() => Show<SignInPage>(Screen.SignIn, ScreenHistory.Arrival.Root, ShowSignIn);

	public void ShowRegister() => Show<RegisterPage>(Screen.Register, ScreenHistory.Arrival.Detail, ShowRegister);

	public void ShowPasswordReset() => Show<PasswordResetPage>(Screen.PasswordReset, ScreenHistory.Arrival.Detail, ShowPasswordReset);

	public void ShowAccount() => Show<AccountPage>(Screen.Account, ScreenHistory.Arrival.Section, ShowAccount);

	public void ShowChatKeyGate() => Show<ChatKeyGatePage>(Screen.ChatKeyGate, ScreenHistory.Arrival.Detail, ShowChatKeyGate);

	public void ShowContacts() => Show<ContactsPage>(Screen.Contacts, ScreenHistory.Arrival.Section, ShowContacts);

	public void ShowTasks() => Show<TasksPage>(Screen.Tasks, ScreenHistory.Arrival.Section, ShowTasks);

	public void ShowCalendar() => Show<CalendarPage>(Screen.Calendar, ScreenHistory.Arrival.Section, ShowCalendar);

	public void ShowCalendarDay()
		=> Show<CalendarPage>(Screen.Calendar, ScreenHistory.Arrival.Section, ShowCalendarDay,
			page => page.ViewModel.OpenOnToday());

	public void ShowInventory() => Show<InventoryPage>(Screen.Inventories, ScreenHistory.Arrival.Section, ShowInventory);

	public void ShowMap() => Show<MapPage>(Screen.Map, ScreenHistory.Arrival.Section, ShowMap);

	public void ShowPlaces() => Show<PlacesPage>(Screen.Places, ScreenHistory.Arrival.Section, ShowPlaces);

	public void ShowPlace(Guid localId)
		=> Show<PlaceDetailPage>(Screen.Place, ScreenHistory.Arrival.Detail,
			() => ShowPlace(localId), page => page.ViewModel.Open(localId));

	public void ShowLocationShares(bool theirs)
		=> Show<LocationSharesPage>(Screen.LocationShares, ScreenHistory.Arrival.Detail,
			() => ShowLocationShares(theirs), page => page.Show(theirs));

	public void ShowInventory(Guid localId, Guid? productId = null)
		=> Show<InventoryDetailPage>(Screen.Inventory, ScreenHistory.Arrival.Detail,
			() => ShowInventory(localId, productId), page => page.ViewModel.Open(localId, productId));

	public void ShowNotifications() => Show<NotificationFeedPage>(Screen.Notifications, ScreenHistory.Arrival.Section, ShowNotifications);

	public void ShowSharedLink(string token)
		=> Show<SharedLinkPage>(Screen.SharedLink, ScreenHistory.Arrival.Detail,
			() => ShowSharedLink(token), page => page.ViewModel.Open(token));

	public void ShowUpdate() => Show<UpdatePage>(Screen.Update, ScreenHistory.Arrival.Section, ShowUpdate);

	// A drawer destination like the sections above it, so arriving resets the stack to [Dashboard, About]
	// rather than piling up behind whatever the reader was reading - see ScreenHistory.Arrival.
	public void ShowAbout() => Show<AboutPage>(Screen.About, ScreenHistory.Arrival.Section, ShowAbout);

	// No ShowNotificationSettings any more: the settings moved onto the account screen - see
	// AccountPage's notification section - so there is no page of their own left to navigate to.
	public void ShowDiagnostics() => Show<DiagnosticsPage>(Screen.Diagnostics, ScreenHistory.Arrival.Detail, ShowDiagnostics);

	public void ShowTaskList(Guid localId)
		=> Show<TaskListDetailPage>(Screen.TaskList, ScreenHistory.Arrival.Detail,
			() => ShowTaskList(localId), page => page.ViewModel.Open(localId));

	public void ShowTaskItem(Guid taskListLocalId, Guid itemId)
		=> Show<TaskItemSummaryPage>(Screen.TaskItem, ScreenHistory.Arrival.Detail,
			() => ShowTaskItem(taskListLocalId, itemId), page => page.ViewModel.Open(taskListLocalId, itemId));

	public void ShowNote(Guid localId)
		=> Show<NoteDetailPage>(Screen.Note, ScreenHistory.Arrival.Detail,
			() => ShowNote(localId), page => page.ViewModel.Open(localId));

	public void ShowCopyReview() => Show<CopyReviewPage>(Screen.CopyReview, ScreenHistory.Arrival.Detail, ShowCopyReview);

	public void ShowCopyHistory(CopyKind kind, Guid localId)
		=> Show<CopyHistoryPage>(Screen.CopyHistory, ScreenHistory.Arrival.Detail,
			() => ShowCopyHistory(kind, localId), page => page.ViewModel.Open(kind, localId));

	public void ShowCalendarEvent(Guid localId)
		=> Show<CalendarEventDetailPage>(Screen.CalendarEvent, ScreenHistory.Arrival.Detail,
			() => ShowCalendarEvent(localId), page => page.ViewModel.Open(localId));

	/// <summary>
	/// A conversation needs to know whose it is, and these screens are resolved from the container rather
	/// than constructed - so the page is told after it exists, before it is shown.
	/// </summary>
	public void ShowConversation(LocalContact contact)
		=> Show<ConversationPage>(Screen.Conversation, ScreenHistory.Arrival.Detail,
			() => ShowConversation(contact), page => page.ViewModel.Open(contact));

	/// <inheritdoc cref="ShowConversation"/>
	public void ShowContactInfo(Guid userId)
		=> Show<ContactInfoPage>(Screen.ContactInfo, ScreenHistory.Arrival.Detail,
			() => ShowContactInfo(userId), page => page.ViewModel.Open(userId));

	public void ShowGroups() => Show<GroupsPage>(Screen.Groups, ScreenHistory.Arrival.Detail, ShowGroups);

	/// <inheritdoc cref="ShowConversation"/>
	public void ShowGroupConversation(LocalChatGroup group)
		=> Show<GroupConversationPage>(Screen.GroupConversation, ScreenHistory.Arrival.Detail,
			() => ShowGroupConversation(group), page => page.ViewModel.Open(group));

	/// <inheritdoc cref="ShowConversation"/>
	public void ShowGroupDetail(LocalChatGroup group)
		=> Show<GroupDetailPage>(Screen.GroupDetail, ScreenHistory.Arrival.Detail,
			() => ShowGroupDetail(group), page => page.ViewModel.Open(group));

	public void ShowDashboard() => Show<DashboardPage>(Screen.Dashboard, ScreenHistory.Arrival.Section, ShowDashboard);

	public void ShowNotes() => Show<NotesPage>(Screen.Notes, ScreenHistory.Arrival.Section, ShowNotes);

	/// <summary>
	/// Shows one screen. <paramref name="arrival"/> says how it joins the history, and
	/// <paramref name="again"/> is what will be run to bring it back - the same call with the same
	/// argument, which is why the parameterised screens above pass a lambda rather than a method group.
	/// </summary>
	private void Show<TPage>(
		Screen screen,
		ScreenHistory.Arrival arrival,
		Action again,
		Action<TPage>? prepare = null) where TPage : Page
		=> MainThread.BeginInvokeOnMainThread(() =>
		{
			if (Application.Current?.Windows.FirstOrDefault() is not { } window)
			{
				return;
			}

			var page = _services.GetRequiredService<TPage>();
			prepare?.Invoke(page);
			window.Page = page;

			// Resolved here rather than taken in the constructor because ScreenHistory needs this class:
			// asking for it up front is a cycle the container cannot build. Everything else this method
			// uses is resolved the same way, so it is the shape this class already has.
			_services.GetRequiredService<ScreenHistory>().Arrived(screen, arrival, again);

			// The navigation bar is one instance shared by every page, so a menu opened over the screen
			// being left would still be open over the one arriving. Orbit.Web closes it on every route
			// change rather than in each thing that navigates - see MainLayout's LocationChanged - and
			// this is the one place mobile changes screens.
			_services.GetRequiredService<NavigationBarViewModel>().CloseMenu();
		});
}
