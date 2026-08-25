using Orbit.Core.Inventory;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>In-memory <see cref="IWarehouseRepository"/> stub for unit tests, with the same owner scoping the real one applies.</summary>
internal sealed class InMemoryWarehouseRepository : IWarehouseRepository
{
    private readonly List<Warehouse> _warehouses = [];

    public Task<IReadOnlyList<Warehouse>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Warehouse>>(_warehouses.Where(warehouse => warehouse.UserId == userId).ToList());

    public Task<Warehouse?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_warehouses.FirstOrDefault(warehouse => warehouse.Id == id && warehouse.UserId == userId));

    public Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken)
    {
        _warehouses.Add(warehouse);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Warehouse warehouse, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        _warehouses.RemoveAll(warehouse => warehouse.Id == id && warehouse.UserId == userId);
        return Task.CompletedTask;
    }

    public Task<Guid?> GetOwnerUserIdAsync(Guid warehouseId, CancellationToken cancellationToken)
        => Task.FromResult(_warehouses.FirstOrDefault(warehouse => warehouse.Id == warehouseId)?.UserId);
}
