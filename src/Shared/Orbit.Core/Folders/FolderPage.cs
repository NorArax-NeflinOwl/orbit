namespace Orbit.Core.Folders;

/// <summary>
/// A page made of cards, as far as the folders on it are concerned. They are not interchangeable: what
/// a page offers as tabs, whether it can make one, and whether it has a Finished tab at all are three
/// answers that differ page by page, and every one of them used to be the same answer given three times.
///
/// "Page" is the browser's word for it; on the phone the same ones are screens and the tabs are entries
/// in a menu. The rules are the same either way, which is why this lives in Orbit.Core beside
/// FolderPlacement rather than next to one client's drawing of them.
/// </summary>
public enum FolderPage
{
    Dashboard,
    Notes,
    Tasks,

    /// <summary>The calendar, whose events are filed the way notes are - added 2026-09-15.</summary>
    Calendar,

    /// <summary>The inventories, whose shelves are filed the same way.</summary>
    Inventories
}

/// <summary>
/// What each page does with folders. Kept beside the enum rather than in each page, because the pages
/// have to disagree consistently - a rule spelled out on the notes and forgotten on the dashboard is
/// how the Finished tab ended up on a page where nothing can be finished.
/// </summary>
public static class FolderPages
{
    /// <summary>
    /// Which stored folders are tabs here. Every page but the dashboard draws its own; the dashboard
    /// draws the scopes of the cards it is made of, because a card has to have a tab to be under.
    ///
    /// The calendar is not among the dashboard's, although the dashboard says what is on today and what
    /// is coming: those two cards answer <em>when</em>, and an event's folder answers <em>which</em>. A
    /// tab that narrowed "today" to one folder would be answering a question nobody asked there.
    /// </summary>
    public static IReadOnlyList<FolderScope> ScopesOn(this FolderPage page) => page switch
    {
        FolderPage.Notes => [FolderScope.Notes],
        FolderPage.Tasks => [FolderScope.Tasks],
        FolderPage.Calendar => [FolderScope.Calendar],
        FolderPage.Inventories => [FolderScope.Inventories],
        _ => [FolderScope.Notes, FolderScope.Tasks, FolderScope.Inventories]
    };

    /// <summary>
    /// The scope a folder made here is given, or null where none can be made. Null is the dashboard's
    /// answer: a folder made there would belong to neither kind of card, so there is nothing that could
    /// ever be filed into it - see FolderScope.
    /// </summary>
    public static FolderScope? MakesFoldersIn(this FolderPage page) => page switch
    {
        FolderPage.Notes => FolderScope.Notes,
        FolderPage.Tasks => FolderScope.Tasks,
        FolderPage.Calendar => FolderScope.Calendar,
        FolderPage.Inventories => FolderScope.Inventories,
        _ => null
    };

    /// <summary>
    /// Whether finished things gather under a tab of their own here. Only on the task lists: a note has
    /// nothing to finish, an event is over rather than done and a shelf is never either, and the
    /// dashboard's answer to a finished list is to stop showing it rather than to file it somewhere -
    /// see Dashboard.razor.
    ///
    /// A page without the tab does not merely hide it. It never asks whether something is finished at
    /// all, so a finished list is in Public or Private there like anything else, instead of being in a
    /// folder the page has no tab for and vanishing.
    /// </summary>
    public static bool HasAFinishedTab(this FolderPage page) => page == FolderPage.Tasks;

    /// <summary>
    /// Whether sealed things gather under a tab of their own here. Everywhere but the calendar: an event
    /// cannot be sealed at all - it is one of the four kinds Orbit does not offer that for - so the tab
    /// there could only ever read zero, which is the same reason the notes have no Finished tab.
    /// </summary>
    public static bool HasAPrivateTab(this FolderPage page) => page != FolderPage.Calendar;

    /// <summary>
    /// Whether things put away gather under a tab of their own here. Everywhere but the dashboard,
    /// which is the page for what somebody is doing now: the archive is where things go to stop being
    /// that, and a tab offering to fill the whole dashboard with them is a way of ending up there by
    /// accident (asked for on 2026-09-18).
    ///
    /// Unlike the Finished tab, dropping this one does <em>not</em> change where anything is placed -
    /// see FolderPlacement, which still answers Archived for something put away. So an archived note is
    /// in a folder the dashboard draws no tab for, and is simply not on the dashboard, which is the
    /// point: it is still in the archive, and its own page is still where it is found again.
    /// </summary>
    public static bool HasAnArchivedTab(this FolderPage page) => page != FolderPage.Dashboard;
}
