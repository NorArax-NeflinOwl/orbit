namespace Orbit.Core.Folders;

/// <summary>
/// Which folder a card is in - the one rule, in one place, because every page made of cards asks it:
/// the browser's dashboard, notes and task lists, the editors that say where the thing they are saving
/// will end up, and the phone's notes and tasks screens.
///
/// It lives in Orbit.Core rather than beside one client because it is the *definition* of where
/// something is, not a drawing of it - two clients working that out separately is two clients that can
/// disagree about which tab a note is under.
///
/// Only one of the built-in folders is stored at all (see Orbit.Core.Folders.BuiltInFolder): which one
/// something is in otherwise follows from what it already is, and the first that applies wins:
///
/// <list type="number">
///   <item>anything its owner has put away is in <b>Archived</b>, filed or not;</item>
///   <item>anything filed under a folder that still exists is in that folder, finished or not;</item>
///   <item>a task list with everything ticked off is in <b>Finished</b>;</item>
///   <item>a sealed item is in <b>Private</b>;</item>
///   <item>everything else is in <b>Public</b>.</item>
/// </list>
///
/// Archiving beats filing, and filing beats finishing. The second used to be the other way round: a list
/// somebody put in "Renovation" left that tab the moment its last entry was ticked off, which reads as
/// the list having been lost - a folder is where its owner decided something goes, and finishing the
/// work is not a decision to file it somewhere else. Finished still gathers everything nobody filed
/// anywhere, which is what it is for.
///
/// The first is the opposite case and settles the same way round for the opposite reason: putting
/// something away <em>is</em> a decision about where it goes, and one taken later than the filing. An
/// archived note still under "Work" would not have been put anywhere. Its folder id is kept untouched,
/// so bringing it back puts it under "Work" again.
/// </summary>
public static class FolderPlacement
{
    /// <summary>
    /// Where this card sits. <paramref name="knownFolderIds"/> is what the reader actually has: a card
    /// filed under a folder that has since gone - or under one belonging to another page - falls back
    /// to a built-in folder rather than disappearing from every tab, which is what a stale id would
    /// otherwise do.
    /// </summary>
    /// <param name="isFinished">
    /// Passed false by every page with no Finished tab, which is how a finished list stays visible
    /// there instead of being filed under a tab that page does not draw - see FolderPages.HasAFinishedTab.
    /// </param>
    /// <param name="isArchived">
    /// Whether its owner has put it away. Unlike the two above this is stored rather than read off what
    /// the thing is - see BuiltInFolder - and no page passes false to hide it: something put away has to
    /// be somewhere it can be found again.
    ///
    /// The dashboard draws no Archived tab (see FolderPages.HasAnArchivedTab) and still passes this,
    /// which is the difference between the two rules: a finished list is placed as unfinished where
    /// there is no tab for it, so it stays on the page, while something put away is placed in the
    /// archive everywhere and is therefore simply not on a page with no tab for it.
    /// </param>
    public static FolderKey Of(
        Guid? folderId, bool isPrivate, bool isFinished, IReadOnlyCollection<Guid> knownFolderIds,
        bool isArchived = false)
    {
        if (isArchived)
        {
            return FolderKey.Of(BuiltInFolder.Archived);
        }

        if (folderId is { } id && knownFolderIds.Contains(id))
        {
            return FolderKey.Of(id);
        }

        if (isFinished)
        {
            return FolderKey.Of(BuiltInFolder.Finished);
        }

        return FolderKey.Of(isPrivate ? BuiltInFolder.Private : BuiltInFolder.Public);
    }
}
