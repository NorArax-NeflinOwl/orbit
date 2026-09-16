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
/// The dashboard has no scope of its own: it shows the cards of several kinds side by side, so it draws
/// their tabs and offers no way to make a folder, there being no dashboard card to file into one.
/// See Orbit.Web.Services.FolderPage, which is that distinction on the client.
///
/// Stored by name (OP_F_SCOPE), so the order here is not a contract and a new kind is added at the end
/// without touching a single stored row - see FolderRepository, which reads a name it does not know as
/// Tasks rather than failing.
/// </summary>
public enum FolderScope
{
    Notes,
    Tasks,

    /// <summary>A tab on the calendar, holding events - added 2026-09-15, the same shape as the two above.</summary>
    Calendar,

    /// <summary>A tab on the inventories, holding shelves.</summary>
    Inventories
}
