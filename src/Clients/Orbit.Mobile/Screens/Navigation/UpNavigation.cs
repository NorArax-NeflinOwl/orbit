namespace Orbit.Mobile.Screens.Navigation;

/// <summary>
/// Where the phone's own back gesture leads, on the platform that has one.
///
/// A hierarchy, not a history. <see cref="IScreenNavigator"/> replaces the window's page and keeps no
/// stack on purpose - signing in must not leave the sign-in screen behind a back gesture, and a build
/// the server has retired must have nothing at all to be swiped past - so there is no previous screen
/// to return to. Every screen has a parent instead, which is the same wherever the reader arrived from.
///
/// The parents are not invented here. They are the destinations the screens' own back buttons already
/// use, so the gesture and the button agree rather than being two answers to the same question. iOS has
/// no system back and never asks.
/// </summary>
public sealed class UpNavigation
{
    /// <summary>
    /// A screen missing from this is one the app is left from: the dashboard, and the two screens that
    /// come before there is anywhere to go.
    /// </summary>
    private static readonly IReadOnlyDictionary<Screen, Screen> Parents = new Dictionary<Screen, Screen>
    {
        [Screen.Register] = Screen.SignIn,
        [Screen.PasswordReset] = Screen.SignIn,
        [Screen.Notes] = Screen.Dashboard,
        [Screen.Tasks] = Screen.Dashboard,
        [Screen.Note] = Screen.Notes,
        [Screen.CopyReview] = Screen.Dashboard,
        [Screen.CopyHistory] = Screen.Dashboard,
        [Screen.TaskList] = Screen.Tasks,
        [Screen.Calendar] = Screen.Dashboard,
        [Screen.CalendarEvent] = Screen.Calendar,
        // The calendar, not the list: this only ever opens from there - see CalendarViewModel.OpenDeadline.
        [Screen.TaskItem] = Screen.Calendar,
        [Screen.Inventories] = Screen.Dashboard,
        [Screen.Inventory] = Screen.Inventories,
        [Screen.Contacts] = Screen.Dashboard,
        [Screen.Conversation] = Screen.Contacts,
        [Screen.ContactInfo] = Screen.Contacts,
        [Screen.Groups] = Screen.Contacts,
        [Screen.GroupConversation] = Screen.Groups,
        [Screen.GroupDetail] = Screen.Groups,
        [Screen.ChatKeyGate] = Screen.Dashboard,
        [Screen.Map] = Screen.Dashboard,
        [Screen.Notifications] = Screen.Dashboard,
        [Screen.SharedLink] = Screen.Dashboard,
        [Screen.Account] = Screen.Dashboard,
        [Screen.Update] = Screen.Dashboard,
        [Screen.Diagnostics] = Screen.Account
    };

    private readonly IScreenNavigator _navigator;

    /// <summary>
    /// Starts at the startup screen because that is what the window opens on, and the navigator is not
    /// what puts it there - see App.CreateWindow in Orbit.Maui.
    /// </summary>
    private Screen _showing = Screen.Startup;

    public UpNavigation(IScreenNavigator navigator) => _navigator = navigator;

    /// <summary>
    /// Told by the navigator as it changes screens, so that "up" is measured from where the reader
    /// actually is rather than from where anything guessed.
    /// </summary>
    public void Showing(Screen screen) => _showing = screen;

    /// <summary>
    /// Moves up one level. False when there is nothing above the current screen, which the caller
    /// answers by letting the platform do what it would have done - on Android, leaving the app.
    /// </summary>
    public bool GoUp()
    {
        if (!Parents.TryGetValue(_showing, out var parent))
        {
            return false;
        }

        Show(parent);
        return true;
    }

    /// <summary>
    /// Only screens taking no argument appear here, and that is not an omission: going up leads to a
    /// list and never to a particular row, so a conversation or an inventory could not be a parent -
    /// there would be nothing to say which one.
    /// </summary>
    private void Show(Screen screen)
    {
        switch (screen)
        {
            case Screen.SignIn: _navigator.ShowSignIn(); return;
            case Screen.Dashboard: _navigator.ShowDashboard(); return;
            case Screen.Notes: _navigator.ShowNotes(); return;
            case Screen.Tasks: _navigator.ShowTasks(); return;
            case Screen.Calendar: _navigator.ShowCalendar(); return;
            case Screen.Inventories: _navigator.ShowInventory(); return;
            case Screen.Contacts: _navigator.ShowContacts(); return;
            case Screen.Groups: _navigator.ShowGroups(); return;
            case Screen.Notifications: _navigator.ShowNotifications(); return;
            case Screen.Account: _navigator.ShowAccount(); return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(screen), screen, "Nothing can go up to this screen - it needs an argument to show.");
        }
    }
}
