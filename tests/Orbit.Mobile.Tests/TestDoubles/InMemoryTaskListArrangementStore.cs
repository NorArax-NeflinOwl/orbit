using Orbit.Mobile.Screens.Tasks;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>What Preferences holds on a device, without one - see PreferencesTaskListArrangementStore.</summary>
internal sealed class InMemoryTaskListArrangementStore : ITaskListArrangementStore
{
    public InMemoryTaskListArrangementStore(TaskListSortOrder remembered = TaskListSortOrder.Priority)
        => RememberedSortOrder = remembered;

    public TaskListSortOrder RememberedSortOrder { get; private set; }

    public IReadOnlyList<Guid> RememberedManualOrder { get; private set; } = [];

    public IReadOnlyList<Guid> RememberedCollapsed { get; private set; } = [];

    public TaskListSortOrder ReadSortOrder() => RememberedSortOrder;

    public void WriteSortOrder(TaskListSortOrder sortOrder) => RememberedSortOrder = sortOrder;

    public IReadOnlyList<Guid> ReadManualOrder() => RememberedManualOrder;

    public void WriteManualOrder(IReadOnlyList<Guid> orderedLocalIds) => RememberedManualOrder = orderedLocalIds;

    public IReadOnlyList<Guid> ReadCollapsed() => RememberedCollapsed;

    public void WriteCollapsed(IReadOnlyList<Guid> collapsedLocalIds) => RememberedCollapsed = collapsedLocalIds;
}
