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
///   <item>a task list with everything ticked off is in <b>Finished</b>, even when its owner filed it somewhere;</item>
///   <item>anything filed under a folder that still exists is in that folder;</item>
///   <item>a sealed item is in <b>Private</b>;</item>
///   <item>everything else is in <b>Public</b>.</item>
/// </list>
/// </summary>
public static class FolderPlacement
{
    /// <summary>
    /// Where this card sits. <paramref name="knownFolderIds"/> is what the reader actually has: a card
    /// filed under a folder that has since gone falls back to a built-in one rather than disappearing
    /// from every tab, which is what a stale id would otherwise do.
    /// </summary>
    public static FolderKey Of(Guid? folderId, bool isPrivate, bool isFinished, IReadOnlyCollection<Guid> knownFolderIds)
    {
        if (isFinished)
        {
            return FolderKey.Of(BuiltInFolder.Finished);
        }

        if (folderId is { } id && knownFolderIds.Contains(id))
        {
            return FolderKey.Of(id);
        }

        return FolderKey.Of(isPrivate ? BuiltInFolder.Private : BuiltInFolder.Public);
    }
}
