using Orbit.Core.Folders;

namespace Orbit.Web.Services;

/// <summary>
/// A page made of cards, as far as the tabs above them are concerned. Three of them, and they are not
/// interchangeable: what a page offers as tabs, whether it can make one, and whether it has a Finished
/// tab at all are three answers that differ page by page, and every one of them used to be the same
/// answer given three times.
/// </summary>
public enum FolderPage
{
    Dashboard,
    Notes,
    Tasks
}

/// <summary>
/// What each page does with folders. Kept beside the enum rather than in each page, because the three
/// pages have to disagree consistently - a rule spelled out on the notes and forgotten on the dashboard
/// is how the Finished tab ended up on a page where nothing can be finished.
/// </summary>
public static class FolderPages
{
    /// <summary>
    /// Which stored folders are tabs here. The dashboard draws both, because it shows notes and task
    /// lists side by side and a card has to have a tab to be under; the other two draw their own.
    /// </summary>
    public static IReadOnlyList<FolderScope> ScopesOn(this FolderPage page) => page switch
    {
        FolderPage.Notes => [FolderScope.Notes],
        FolderPage.Tasks => [FolderScope.Tasks],
        _ => [FolderScope.Notes, FolderScope.Tasks]
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
        _ => null
    };

    /// <summary>
    /// Whether finished things gather under a tab of their own here. Only on the task lists: a note has
    /// nothing to finish, and the dashboard's answer to a finished list is to stop showing it rather
    /// than to file it somewhere - see Dashboard.razor.
    ///
    /// A page without the tab does not merely hide it. It never asks whether something is finished at
    /// all, so a finished list is in Public or Private there like anything else, instead of being in a
    /// folder the page has no tab for and vanishing.
    /// </summary>
    public static bool HasAFinishedTab(this FolderPage page) => page == FolderPage.Tasks;
}
