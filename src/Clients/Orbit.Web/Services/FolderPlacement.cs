using Orbit.Core.Folders;

namespace Orbit.Web.Services;

/// <summary>
/// Which folder a card is in - the one rule, in one place, because four pages ask it: the dashboard's
/// two cards, the notes, the task lists, and the editors that say where the thing they are saving will
/// end up.
///
/// Nothing is stored about the built-in folders (see Orbit.Core.Folders.BuiltInFolder); which one
/// something is in follows from what it already is, and the first that applies wins:
///
/// <list type="number">
///   <item>anything filed under a folder that still exists is in that folder, finished or not;</item>
///   <item>a task list with everything ticked off is in <b>Finished</b>;</item>
///   <item>a sealed item is in <b>Private</b>;</item>
///   <item>everything else is in <b>Public</b>.</item>
/// </list>
///
/// Filing beats finishing, and it used to be the other way round. A list somebody put in "Renovation"
/// left that tab the moment its last entry was ticked off, which reads as the list having been lost: a
/// folder is where its owner decided something goes, and finishing the work is not a decision to file
/// it somewhere else. Finished still gathers everything nobody filed anywhere, which is what it is for.
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
    public static FolderKey Of(Guid? folderId, bool isPrivate, bool isFinished, IReadOnlyCollection<Guid> knownFolderIds)
    {
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
