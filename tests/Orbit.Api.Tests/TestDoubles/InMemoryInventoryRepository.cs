using Orbit.Core.Inventories;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>In-memory <see cref="IInventoryRepository"/> stub for unit tests, with the same owner scoping the real one applies.</summary>
internal sealed class InMemoryInventoryRepository : IInventoryRepository
{
    private readonly List<Inventory> _inventories = [];

    public Task<IReadOnlyList<Inventory>> GetAllAsync(
        Guid userId, DateTimeOffset? updatedSinceUtc, CancellationToken cancellationToken)
    {
        var matching = _inventories.Where(inventory => inventory.UserId == userId);
        if (updatedSinceUtc is not null)
        {
            matching = matching.Where(inventory => inventory.UpdatedAtUtc >= updatedSinceUtc.Value);
        }

        return Task.FromResult<IReadOnlyList<Inventory>>(matching.ToList());
    }

    public Task<Inventory?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_inventories.FirstOrDefault(inventory => inventory.Id == id && inventory.UserId == userId));

    public Task AddAsync(Inventory inventory, CancellationToken cancellationToken)
    {
        _inventories.Add(inventory);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Inventory inventory, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Counted rather than performed - the real one writes three columns and leaves the rest of the row
    /// alone, and what a test can check here is that a lock took this path.
    /// </summary>
    public Task UpdateLockAsync(Inventory inventory, CancellationToken cancellationToken)
    {
        LockSaves++;
        return Task.CompletedTask;
    }

    /// <summary>How many times a lock was saved on its own - see UpdateLockAsync.</summary>
    public int LockSaves { get; private set; }

    public Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        _inventories.RemoveAll(inventory => inventory.Id == id && inventory.UserId == userId);
        return Task.CompletedTask;
    }

    public Task<Guid?> GetOwnerUserIdAsync(Guid inventoryId, CancellationToken cancellationToken)
        => Task.FromResult(_inventories.FirstOrDefault(inventory => inventory.Id == inventoryId)?.UserId);
}
