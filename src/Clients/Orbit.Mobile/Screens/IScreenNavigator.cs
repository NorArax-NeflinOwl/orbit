using Orbit.Mobile.Data;

namespace Orbit.Mobile.Screens;

/// <summary>
/// Where a screen can send the reader next. The one thing the view models needed from the app head, and
/// the reason they could not be tested while they lived in it: navigation is the only part of a screen
/// that is genuinely a platform concern, so it is the only part left behind an interface.
///
/// Every destination is named rather than addressed by route, because these screens replace each other
/// outright instead of forming a stack - see AppNavigator in Orbit.Maui for why.
/// </summary>
public interface IScreenNavigator
{
    void ShowSignIn();

    void ShowRegister();

    void ShowAccount();

    void ShowChatKeyGate();

    void ShowContacts();

    void ShowConversation(LocalContact contact);

    void ShowGroups();

    void ShowGroupConversation(LocalChatGroup group);

    void ShowGroupDetail(LocalChatGroup group);

    void ShowNotes();

    void ShowTasks();

    void ShowTaskList(Guid localId);

    void ShowCalendar();

    void ShowInventory();

    void ShowMap();

    void ShowWarehouse(Guid localId);

    /// <summary>What happened while the reader was elsewhere - see NotificationFeedViewModel.</summary>
    void ShowNotifications();
}
