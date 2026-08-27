namespace Orbit.Mobile.Screens.Navigation;

/// <summary>
/// Every screen the app can be showing, as the name that "up" is worked out from.
///
/// A separate vocabulary from <see cref="IScreenNavigator"/>'s methods, because several of those take
/// an argument and none of these do: going up from a conversation leads to the contact list, never to
/// a particular contact, so nothing here has to say which conversation was open.
/// </summary>
public enum Screen
{
    /// <summary>Where the app opens, before it knows whether it is still allowed to run.</summary>
    Startup,
    SignIn,
    Register,
    Dashboard,
    Notes,
    Tasks,
    TaskList,
    Calendar,
    Inventory,
    Warehouse,
    Contacts,
    Conversation,
    Groups,
    GroupConversation,
    GroupDetail,
    ChatKeyGate,
    Map,
    Notifications,
    NotificationSettings,
    Account,
    Diagnostics
}
