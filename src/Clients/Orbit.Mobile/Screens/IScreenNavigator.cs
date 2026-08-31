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

    /// <summary>Where the app opens - everything on the reader's plate, the same as Orbit.Web's landing page.</summary>
    void ShowDashboard();

    void ShowNotes();

    /// <summary>One note, opened from the list - see NoteDetailViewModel.</summary>
    void ShowNote(Guid localId);

    /// <summary>
    /// The copies taken while offline, each beside the note it came from - opened when the connection
    /// is back and there is something to decide. See NoteCopyReviewViewModel.
    /// </summary>
    void ShowNoteCopyReview();

    /// <summary>Copies kept rather than merged, and what each came from - see NoteHistoryViewModel.</summary>
    void ShowNoteHistory();

    void ShowTasks();

    void ShowTaskList(Guid localId);

    /// <summary>
    /// One entry on its own, opened from the calendar when it is somewhere as well as at some time -
    /// see TaskItemSummaryViewModel. Carries the list too, because that is what the phone stores an
    /// entry inside.
    /// </summary>
    void ShowTaskItem(Guid taskListLocalId, Guid itemId);

    void ShowCalendar();

    /// <summary>One event, opened from the calendar - see CalendarEventDetailViewModel.</summary>
    void ShowCalendarEvent(Guid localId);

    void ShowInventory();

    void ShowMap();

    void ShowWarehouse(Guid localId);

    /// <summary>What happened while the reader was elsewhere - see NotificationFeedViewModel.</summary>
    void ShowNotifications();

    /// <summary>Where a newer Orbit comes from - see UpdateViewModel.</summary>
    void ShowUpdate();

    /// <summary>What Orbit is allowed to interrupt the reader with - see NotificationSettingsViewModel.</summary>
    /// <summary>The app's own log, and the one way it leaves the phone - see DiagnosticsViewModel.</summary>
    void ShowDiagnostics();
}
