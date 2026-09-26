using Orbit.Core.Folders;

namespace Orbit.Web.Services;

/// <summary>
/// Which folder tabs a page offers, and whether the row of them is worth drawing at all.
///
/// Its own class rather than a property on FolderTabs because two things need the answer and only one
/// of them is the row. On a phone the row folds into a single button (see PhoneToolbar), and that
/// button is drawn by the page rather than by the row - so a page whose row has nothing to choose
/// between would otherwise still offer a button that opens an empty panel.
/// </summary>
public static class FolderTabRow
{
    /// <param name="holdsAnything">
    /// Whether a tab has anything under it, where the page can say - see FolderTabs.HoldsAnything.
    /// Null keeps every tab, which is what every page but the dashboard wants.
    /// </param>
    public static IReadOnlyList<FolderKey> On(
        FolderPage page, FolderState folders, DashboardCardPreferences cardPreferences,
        Func<FolderKey, bool>? holdsAnything)
    {
        // The built-in ones first, in the order somebody reads them - the lot, what is sealed, what is
        // finished, what has been put away. Not all of them are on every page: see
        // FolderPages.HasAFinishedTab, HasAPrivateTab and HasAnArchivedTab for which and why.
        var builtIn = new List<FolderKey> { FolderKey.Of(BuiltInFolder.All) };
        if (page.HasAPrivateTab())
        {
            builtIn.Add(FolderKey.Of(BuiltInFolder.Private));
        }

        if (page.HasAFinishedTab())
        {
            builtIn.Add(FolderKey.Of(BuiltInFolder.Finished));
        }

        if (page.HasAnArchivedTab())
        {
            builtIn.Add(FolderKey.Of(BuiltInFolder.Archived));
        }

        // A folder somebody took off the dashboard is not a tab there - see
        // DashboardCardPreferences.IsFolderShown. Only there: the page the folder belongs to keeps
        // drawing it, because that is where the folder is for.
        var shown = folders.FoldersOn(page)
            .Where(folder => page != FolderPage.Dashboard || cardPreferences.IsFolderShown(folder.Id));

        // And on the dashboard one tab per name, not one per folder. A folder holds one kind of thing,
        // so "Home" on the task lists and "Home" on the inventories are two stored folders; here, where
        // both kinds are drawn side by side, they are one answer to "show me what is at home". Two
        // identical tabs, each showing half of it, is what the user found on 2026-09-20. The first of
        // them stands for the rest - see FolderState.FoldersCalledTheSameAs and Dashboard.IsUnder,
        // which reads every card of that name under it.
        var made = (page == FolderPage.Dashboard
                ? shown.GroupBy(folder => folder.Name.Trim(), StringComparer.CurrentCultureIgnoreCase)
                    .Select(sameName => sameName.First())
                : shown)
            .Select(folder => FolderKey.Of(folder.Id));

        return
        [
            .. builtIn.Concat(made)
                .Where(tab => IsWorthATab(tab, page, folders, holdsAnything))
        ];
    }

    /// <summary>
    /// Whether the row is worth having at all. One tab left means there is nowhere else to go, and a
    /// lone "All" above the page is a control that can only be pressed to stay where you already are.
    /// Only where the page prunes its tabs - everywhere else the row is also where a folder is made, so
    /// it stays however few tabs are in it.
    /// </summary>
    public static bool IsWorthDrawing(
        FolderPage page, FolderState folders, DashboardCardPreferences cardPreferences,
        Func<FolderKey, bool>? holdsAnything)
        => holdsAnything is null || On(page, folders, cardPreferences, holdsAnything).Count > 1;

    /// <summary>
    /// All always stays: it is where everything the page holds is, and a page with no tab at all is a
    /// page nothing can be chosen on. So does whatever is open, empty or not - taking the tab out from
    /// under the reader would leave them looking at a folder they could not see they were in, with no
    /// way back to anywhere else.
    /// </summary>
    private static bool IsWorthATab(
        FolderKey tab, FolderPage page, FolderState folders, Func<FolderKey, bool>? holdsAnything)
        => holdsAnything is null
            || tab == FolderKey.Of(BuiltInFolder.All)
            || tab == folders.ChosenOn(page)
            || holdsAnything(tab);
}
