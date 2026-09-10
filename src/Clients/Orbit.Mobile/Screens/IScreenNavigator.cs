using Orbit.Mobile.Data;
using Orbit.Mobile.Notifications;

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

    /// <summary>
    /// Asking for a code and setting a new password, for somebody who cannot get past the sign-in
    /// screen - see PasswordResetViewModel.
    /// </summary>
    void ShowPasswordReset();

    void ShowAccount();

    void ShowChatKeyGate();

    void ShowContacts();

    void ShowConversation(LocalContact contact);

    /// <summary>
    /// Who somebody is, apart from what they have said - see ContactInfoViewModel. Named by the account
    /// rather than by a stored contact, because it is also opened for somebody this phone has never
    /// spoken to.
    /// </summary>
    void ShowContactInfo(Guid userId);

    void ShowGroups();

    void ShowGroupConversation(LocalChatGroup group);

    void ShowGroupDetail(LocalChatGroup group);

    /// <summary>Where the app opens - everything on the reader's plate, the same as Orbit.Web's landing page.</summary>
    void ShowDashboard();

    void ShowNotes();

    /// <summary>One note, opened from the list - see NoteDetailViewModel.</summary>
    void ShowNote(Guid localId);

    /// <summary>
    /// The copies taken while offline, each beside the thing it came from - opened when the connection
    /// is back and there is something to decide. See CopyReviewViewModel.
    /// </summary>
    void ShowCopyReview();

    /// <summary>
    /// One thing's history: the copies taken from it, and where each came from. Opened from the thing
    /// itself - see CopyHistoryViewModel for why it is per thing rather than one global list.
    /// </summary>
    void ShowCopyHistory(Data.CopyKind kind, Guid localId);

    void ShowTasks();

    void ShowTaskList(Guid localId);

    /// <summary>
    /// One entry on its own, opened from the calendar when it is somewhere as well as at some time -
    /// see TaskItemSummaryViewModel. Carries the list too, because that is what the phone stores an
    /// entry inside.
    /// </summary>
    void ShowTaskItem(Guid taskListLocalId, Guid itemId);

    void ShowCalendar();

    /// <summary>
    /// The calendar opened on today, by the hour. Where the dashboard's own summary of the day leads:
    /// it counts what is happening today, so pressing it should show today rather than the month it
    /// happens to be in.
    /// </summary>
    void ShowCalendarDay();

    /// <summary>One event, opened from the calendar - see CalendarEventDetailViewModel.</summary>
    void ShowCalendarEvent(Guid localId);

    void ShowInventory();

    void ShowMap();

    /// <summary>The places kept on the map - see PlacesViewModel.</summary>
    void ShowPlaces();

    /// <summary>One of them, opened from that list - see PlaceDetailViewModel.</summary>
    void ShowPlace(Guid localId);

    /// <param name="productId">
    /// Which product this was opened for, when it was opened from something that meant one - an errand
    /// naming the shelf it is about, or a search that found the thing on it. The shelf marks that row
    /// and opens on it; null when the inventory was opened for its own sake. Orbit.Web passes the same
    /// thing as ?highlight= - see InventorySummary.
    /// </param>
    void ShowInventory(Guid localId, Guid? productId = null);

    /// <summary>What happened while the reader was elsewhere - see NotificationFeedViewModel.</summary>
    void ShowNotifications();

    /// <summary>
    /// What is behind a public link somebody followed into the app, named by the token in it - see
    /// SharedLinkViewModel.
    /// </summary>
    void ShowSharedLink(string token);

    /// <summary>
    /// Something offered to this reader and not yet taken up, named by the offer a notification's path
    /// carries - see InvitationViewModel, and NotificationDestination for where the offer comes from.
    /// </summary>
    void ShowInvitation(InvitationOffer offer);

    /// <summary>Where a newer Orbit comes from - see UpdateViewModel.</summary>
    void ShowUpdate();

    /// <summary>The app's own log, and the one way it leaves the phone - see DiagnosticsViewModel.</summary>
    void ShowDiagnostics();

    /// <summary>
    /// What Orbit is, which build this one is, and where the documents about it are - see
    /// AboutViewModel. A screen of its own rather than a fold-out at the foot of the drawer, which is
    /// where it used to be: it is one of the drawer's entries, and an entry that expands in place is
    /// the only one that does not take the reader anywhere.
    /// </summary>
    void ShowAbout();

    /// <summary>
    /// One of the map's two lists of people: who can see where the reader is, or who is letting the
    /// reader see where they are. A screen of its own, because a list of names drawn over the map
    /// covers the thing it is about - see MapViewModel.
    /// </summary>
    /// <param name="theirs">True for the positions shared with this reader; false for their own shares.</param>
    void ShowLocationShares(bool theirs);
}
