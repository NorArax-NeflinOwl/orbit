using Orbit.Core.Inventory;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>In-memory <see cref="IWarehouseRepository"/> stub for unit tests, with the same owner scoping the real one applies.</summary>
internal sealed class InMemoryWarehouseRepository : IWarehouseRepository
{
    private readonly List<Warehouse> _warehouses = [];

    public Task<IReadOnlyList<Warehouse>> GetAllAsync(
        Guid userId, DateTimeOffset? updatedSinceUtc, CancellationToken cancellationToken)
    {
        var matching = _warehouses.Where(warehouse => warehouse.UserId == userId);
        if (updatedSinceUtc is not null)
        {
            matching = matching.Where(warehouse => warehouse.UpdatedAtUtc >= updatedSinceUtc.Value);
        }

        return Task.FromResult<IReadOnlyList<Warehouse>>(matching.ToList());
    }

    public Task<Warehouse?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_warehouses.FirstOrDefault(warehouse => warehouse.Id == id && warehouse.UserId == userId));

    public Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken)
    {
        _warehouses.Add(warehouse);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Warehouse warehouse, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Counted rather than performed - the real one writes three columns and leaves the rest of the row
    /// alone, and what a test can check here is that a lock took this path.
    /// </summary>
    public Task UpdateLockAsync(Warehouse warehouse, CancellationToken cancellationToken)
    {
        LockSaves++;
        return Task.CompletedTask;
    }

    /// <summary>How many times a lock was saved on its own - see UpdateLockAsync.</summary>
    public int LockSaves { get; private set; }

    public Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        _warehouses.RemoveAll(warehouse => warehouse.Id == id && warehouse.UserId == userId);
        return Task.CompletedTask;
    }

    public Task<Guid?> GetOwnerUserIdAsync(Guid warehouseId, CancellationToken cancellationToken)
        => Task.FromResult(_warehouses.FirstOrDefault(warehouse => warehouse.Id == warehouseId)?.UserId);
}
