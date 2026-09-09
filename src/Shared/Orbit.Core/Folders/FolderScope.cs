namespace Orbit.Core.Folders;

/// <summary>
/// Which page a folder somebody made is a tab on. A folder is a place to put one kind of thing, and
/// the two kinds are filed from different forms and read on different pages: a tab called "Work" on
/// the notes and a tab called "Work" on the task lists are two folders, not one seen twice.
///
/// Stored on the folder rather than worked out from what is in it, because an empty folder still has
/// to know which page it belongs on - a tab that only appeared once somebody had filed something
/// under it could never be filed into in the first place.
///
/// The dashboard has no scope of its own: it shows notes and task lists side by side, so it draws both
/// scopes' tabs and offers no way to make a folder, there being no dashboard card to file into one.
/// See Orbit.Web.Services.FolderPage, which is that distinction on the client.
/// </summary>
public enum FolderScope
{
    Notes,
    Tasks
}
