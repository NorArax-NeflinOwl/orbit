namespace Orbit.Core.Folders;

/// <summary>
/// Whether the tab a calendar is being read under is one the deadlines belong on.
///
/// A deadline is not an appointment: it is an entry on a task list, drawn on the calendar because it
/// falls due there. It has no folder of this page's - what folder it has belongs to its list, on the
/// other page and in another scope (see <see cref="FolderScope"/>) - and nothing on the calendar can
/// put one away, because putting away belongs to the list it is on.
///
/// So it is drawn where anything unfiled and not put away is drawn, which is All, and nowhere else.
/// Still nowhere else after All widened on 2026-09-24 to hold every folder's contents: what widened is
/// which <em>appointments</em> that tab gathers, and a deadline has no folder here to be gathered by.
/// Both clients used to draw every deadline under every tab: the Archived tab showed a page of things
/// that had not been archived at all, and a folder somebody made for appointments showed them too.
/// Reported on 2026-09-25 against the calendar's Archived tab, in the list and in the grid alike.
/// </summary>
public static class CalendarDeadlineTab
{
    public static bool ShowsDeadlines(FolderKey chosen) => chosen == FolderKey.Default;
}
