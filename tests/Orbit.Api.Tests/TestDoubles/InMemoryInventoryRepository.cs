using Orbit.Core.Inventory;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IInventoryRepository"/> stub for unit tests that need real add/lookup/update
/// behavior, including per-warehouse scoping, without spinning up Postgres.
/// </summary>
internal sealed class InMemoryInventoryRepository : IInventoryRepository
{
    private readonly List<InventoryItem> _items = [];

    public Task<IReadOnlyList<InventoryItem>> GetAllAsync(Guid warehouseId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<InventoryItem>>(_items.Where(item => item.WarehouseId == warehouseId).ToList());

    public Task<InventoryItem?> GetByIdAsync(Guid warehouseId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_items.FirstOrDefault(item => item.Id == id && item.WarehouseId == warehouseId));

    public Task AddAsync(InventoryItem item, CancellationToken cancellationToken)
    {
        _items.Add(item);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(InventoryItem item, CancellationToken cancellationToken)
    {
        // Handlers mutate the same InventoryItem instance this repository already holds a reference
        // to, so there is nothing to replace here - mirrors InMemoryNoteRepository/InMemoryTaskRepository.
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid warehouseId, Guid id, CancellationToken cancellationToken)
    {
        _items.RemoveAll(item => item.Id == id && item.WarehouseId == warehouseId);
        return Task.CompletedTask;
    }

    public Task DeleteAllInWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        _items.RemoveAll(item => item.WarehouseId == warehouseId);
        return Task.CompletedTask;
    }
}
