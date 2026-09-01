using Orbit.Mobile.Screens.Tasks;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>How each checklist is read, kept for as long as one test runs.</summary>
internal sealed class InMemoryChecklistReadingStore : IChecklistReadingStore
{
    private readonly Dictionary<Guid, ChecklistReading> _byTaskList = [];

    public ChecklistReading Read(Guid taskListLocalId)
        => _byTaskList.GetValueOrDefault(taskListLocalId, ChecklistReading.Default);

    public void Write(Guid taskListLocalId, ChecklistReading reading) => _byTaskList[taskListLocalId] = reading;
}
