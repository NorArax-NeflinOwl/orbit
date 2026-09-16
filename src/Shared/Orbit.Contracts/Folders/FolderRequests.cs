namespace Orbit.Contracts.Folders;

/// <param name="Scope">
/// The page the tab is being made on - "Notes" or "Tasks", see Orbit.Core.Folders.FolderScope. It has
/// no default: a folder made without saying which page it is on would be a tab nothing on that page
/// could ever be filed into, and guessing one of the two would put half of them on the wrong page.
/// </param>
public sealed record CreateFolderRequest(string Name, string Scope);

public sealed record RenameFolderRequest(string Name);

/// <summary>
/// Where to file one note or one task list. Null means "in no folder of its own", which puts it back
/// under whichever built-in folder it belongs to - see Orbit.Core.Folders.BuiltInFolder.
///
/// Its own request, sent to its own endpoint, rather than a field on the update: an update carries the
/// whole item, so a client that had never heard of folders would empty this every time it saved
/// something - see MoveNoteToFolderCommand.
/// </summary>
public sealed record MoveToFolderRequest(Guid? FolderId);

/// <summary>
/// Whether to put one thing away or bring it back - see Orbit.Core.Folders.BuiltInFolder.Archived, the
/// tab it gathers under while it is away. One request for both directions, which is what the command
/// behind it takes and for the reason given there.
///
/// Its own request and its own endpoint, exactly as filing is, and for the same reason: an update
/// carries the whole item, so a client that had never heard of archiving would bring back everything
/// its owner had put away, every time it saved.
/// </summary>
public sealed record ArchiveRequest(bool IsArchived);
