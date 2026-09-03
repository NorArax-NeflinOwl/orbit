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

    /// <summary>Getting back in without the password - see PasswordResetViewModel.</summary>
    PasswordReset,
    Dashboard,
    Notes,
    Note,

    /// <summary>Copies taken offline, waiting to be chosen between - see CopyReviewViewModel.</summary>
    CopyReview,

    /// <summary>Copies kept on purpose, and what each came from - see CopyHistoryViewModel.</summary>
    CopyHistory,
    Tasks,
    TaskList,
    TaskItem,
    Calendar,
    CalendarEvent,
    Inventory,
    Warehouse,
    Contacts,
    Conversation,

    /// <summary>Who somebody is, apart from what they have said - see ContactInfoViewModel.</summary>
    ContactInfo,
    Groups,
    GroupConversation,
    GroupDetail,
    ChatKeyGate,
    Map,
    Notifications,

    /// <summary>Something somebody sent a link to, read without being in the account - see SharedLinkViewModel.</summary>
    SharedLink,
    Account,
    Update,
    Diagnostics
}
