namespace Orbit.Contracts.Folders;

public sealed record CreateFolderRequest(string Name);

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
