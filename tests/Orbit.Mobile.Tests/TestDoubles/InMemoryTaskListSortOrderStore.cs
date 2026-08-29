using Orbit.Mobile.Screens.Tasks;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>What Preferences holds on a device, without one - see PreferencesTaskListSortOrderStore.</summary>
internal sealed class InMemoryTaskListSortOrderStore : ITaskListSortOrderStore
{
    public InMemoryTaskListSortOrderStore(TaskListSortOrder remembered = TaskListSortOrder.Priority)
        => Remembered = remembered;

    public TaskListSortOrder Remembered { get; private set; }

    public TaskListSortOrder Read() => Remembered;

    public void Write(TaskListSortOrder sortOrder) => Remembered = sortOrder;
}
