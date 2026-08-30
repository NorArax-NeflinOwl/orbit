using Orbit.Mobile.Data;
using Orbit.Mobile.Screens;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// Remembers where a view model tried to send the reader, instead of swapping a window's page. The only
/// thing standing between the screens and an ordinary test project - see <see cref="IScreenNavigator"/>.
/// </summary>
internal sealed class RecordingScreenNavigator : IScreenNavigator
{
    private readonly List<string> _destinations = [];

    /// <summary>Where it was sent, in order, named after the method that sent it.</summary>
    public IReadOnlyList<string> Destinations => _destinations;

    public string? LastDestination => _destinations.Count == 0 ? null : _destinations[^1];

    /// <summary>Which conversation, group or task list it was sent to - not just that it was sent.</summary>
    public LocalContact? LastContact { get; private set; }

    public LocalChatGroup? LastGroup { get; private set; }

    public Guid? LastTaskListId { get; private set; }

    /// <summary>
    /// Which warehouse it was sent to. Kept because the inventory search answers "which warehouse is
    /// this in" - a double that dropped the id would let it open the wrong one and still pass.
    /// </summary>
    public Guid? LastWarehouseId { get; private set; }

    public void ShowSignIn() => _destinations.Add(nameof(ShowSignIn));

    public void ShowRegister() => _destinations.Add(nameof(ShowRegister));

    public void ShowAccount() => _destinations.Add(nameof(ShowAccount));

    public void ShowChatKeyGate() => _destinations.Add(nameof(ShowChatKeyGate));

    public void ShowContacts() => _destinations.Add(nameof(ShowContacts));

    public void ShowConversation(LocalContact contact)
    {
        LastContact = contact;
        _destinations.Add(nameof(ShowConversation));
    }

    public void ShowGroups() => _destinations.Add(nameof(ShowGroups));

    public void ShowGroupConversation(LocalChatGroup group)
    {
        LastGroup = group;
        _destinations.Add(nameof(ShowGroupConversation));
    }

    public void ShowGroupDetail(LocalChatGroup group) => _destinations.Add(nameof(ShowGroupDetail));

    public void ShowDashboard() => _destinations.Add(nameof(ShowDashboard));

    public void ShowNotes() => _destinations.Add(nameof(ShowNotes));

    public void ShowTasks() => _destinations.Add(nameof(ShowTasks));

    public void ShowTaskList(Guid localId)
    {
        LastTaskListId = localId;
        _destinations.Add(nameof(ShowTaskList));
    }

    /// <summary>Which entry was opened on its own, and off which list - see TaskItemSummaryViewModel.</summary>
    public (Guid TaskListLocalId, Guid ItemId)? LastTaskItem { get; private set; }

    public void ShowTaskItem(Guid taskListLocalId, Guid itemId)
    {
        LastTaskItem = (taskListLocalId, itemId);
        _destinations.Add(nameof(ShowTaskItem));
    }

    /// <summary>Which note was opened, so a test can check the list led to the right one.</summary>
    public Guid? LastNoteId { get; private set; }

    public void ShowNote(Guid localId)
    {
        LastNoteId = localId;
        _destinations.Add(nameof(ShowNote));
    }

    public void ShowCalendar() => _destinations.Add(nameof(ShowCalendar));

    /// <summary>Which event was opened, so a test can check the calendar led to the right one.</summary>
    public Guid? LastCalendarEventId { get; private set; }

    public void ShowCalendarEvent(Guid localId)
    {
        LastCalendarEventId = localId;
        _destinations.Add(nameof(ShowCalendarEvent));
    }

    public void ShowInventory() => _destinations.Add(nameof(ShowInventory));

    public void ShowUpdate() => _destinations.Add(nameof(ShowUpdate));

    public void ShowMap() => _destinations.Add(nameof(ShowMap));

    public void ShowWarehouse(Guid localId)
    {
        LastWarehouseId = localId;
        _destinations.Add(nameof(ShowWarehouse));
    }

    public void ShowNotifications() => _destinations.Add(nameof(ShowNotifications));

    public void ShowDiagnostics() => _destinations.Add(nameof(ShowDiagnostics));
}
