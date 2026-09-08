namespace Orbit.Mobile.Screens.Navigation;

/// <summary>
/// Which entry in the drawer a screen belongs under.
///
/// The drawer marks where the reader is, and a note is still "Notes" even though the notes list is not
/// what is on screen. Without this the mark would go out whenever anybody opened anything, which reads
/// as having left the section rather than having gone further into it.
///
/// Only the screens that sit under something appear. Anything missing is its own section, or is not in
/// the drawer at all - the account screen, a shared link, the update screen.
/// </summary>
public static class Sections
{
    private static readonly IReadOnlyDictionary<Screen, Screen> Of = new Dictionary<Screen, Screen>
    {
        [Screen.Note] = Screen.Notes,
        [Screen.TaskList] = Screen.Tasks,
        [Screen.TaskItem] = Screen.Tasks,
        [Screen.CalendarEvent] = Screen.Calendar,
        [Screen.Inventory] = Screen.Inventories,
        [Screen.Conversation] = Screen.Contacts,
        [Screen.ContactInfo] = Screen.Contacts,
        [Screen.Groups] = Screen.Contacts,
        [Screen.GroupConversation] = Screen.Contacts,
        [Screen.GroupDetail] = Screen.Contacts,
        [Screen.ChatKeyGate] = Screen.Contacts
    };

    public static Screen For(Screen screen) => Of.TryGetValue(screen, out var section) ? section : screen;
}
