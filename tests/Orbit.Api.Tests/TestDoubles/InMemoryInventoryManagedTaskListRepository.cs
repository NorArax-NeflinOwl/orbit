using Orbit.Core.Inventories;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>In-memory <see cref="IInventoryManagedTaskListRepository"/> stub for unit tests.</summary>
internal sealed class InMemoryInventoryManagedTaskListRepository : IInventoryManagedTaskListRepository
{
    private readonly Dictionary<Guid, Guid> _taskListIdByInventoryId = [];

    public Task<Guid?> GetTaskListIdAsync(Guid inventoryId, CancellationToken cancellationToken)
        => Task.FromResult(_taskListIdByInventoryId.TryGetValue(inventoryId, out var taskListId) ? taskListId : (Guid?)null);

    public Task<Guid?> GetInventoryIdAsync(Guid taskListId, CancellationToken cancellationToken)
        => Task.FromResult(_taskListIdByInventoryId
            .Where(tracked => tracked.Value == taskListId)
            .Select(tracked => (Guid?)tracked.Key)
            .FirstOrDefault());

    public Task SetTaskListIdAsync(Guid inventoryId, Guid taskListId, CancellationToken cancellationToken)
    {
        _taskListIdByInventoryId[inventoryId] = taskListId;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Drops the entry rather than storing an empty id, which is the same answer GetTaskListIdAsync
    /// gives either way - see the real repository, which empties the id to keep the settings beside it.
    /// </summary>
    public Task ClearTaskListIdAsync(Guid inventoryId, CancellationToken cancellationToken)
    {
        _taskListIdByInventoryId.Remove(inventoryId);
        return Task.CompletedTask;
    }

    private readonly Dictionary<Guid, RestockListSettings> _settingsByInventoryId = [];

    public Task<RestockListSettings> GetSettingsAsync(Guid inventoryId, CancellationToken cancellationToken)
        => Task.FromResult(_settingsByInventoryId.GetValueOrDefault(inventoryId, RestockListSettings.Default));

    public Task SetSettingsAsync(Guid inventoryId, RestockListSettings settings, CancellationToken cancellationToken)
    {
        _settingsByInventoryId[inventoryId] = settings;
        return Task.CompletedTask;
    }
}
