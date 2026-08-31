using Orbit.Mobile.Data;

namespace Orbit.Mobile.Screens.Copies;

/// <summary>
/// Where a copy of each kind opens, and what each kind is called. Both windows here hold all four
/// kinds at once, and both had the same switch in them until this took it.
/// </summary>
public static class CopyDestination
{
    public static void Show(IScreenNavigator navigator, CopyKind kind, Guid localId)
    {
        switch (kind)
        {
            case CopyKind.Note:
                navigator.ShowNote(localId);
                return;
            case CopyKind.TaskList:
                navigator.ShowTaskList(localId);
                return;
            case CopyKind.CalendarEvent:
                navigator.ShowCalendarEvent(localId);
                return;
            case CopyKind.Warehouse:
                navigator.ShowWarehouse(localId);
                return;
        }
    }

    /// <summary>The dictionary key for what this kind is called - see <see cref="Localization.Translations"/>.</summary>
    public static string Describe(CopyKind kind)
        => kind switch
        {
            CopyKind.Note => "Note",
            CopyKind.TaskList => "Task list",
            CopyKind.CalendarEvent => "Appointment",
            _ => "Warehouse"
        };
}
