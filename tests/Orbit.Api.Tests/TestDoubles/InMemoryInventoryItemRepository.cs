using Orbit.Core.Inventories;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IInventoryItemRepository"/> stub for unit tests that need real add/lookup/update
/// behavior, including per-inventory scoping, without spinning up Postgres.
/// </summary>
internal sealed class InMemoryInventoryItemRepository : IInventoryItemRepository
{
    private readonly List<InventoryItem> _items = [];

    /// <summary>As arranged, then by name - the order InventoryItemRepository reads a shelf back in.</summary>
    public Task<IReadOnlyList<InventoryItem>> GetAllAsync(Guid inventoryId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<InventoryItem>>(
            [.. _items.Where(item => item.InventoryId == inventoryId).OrderBy(item => item.Position).ThenBy(item => item.Name)]);

    public Task<InventoryItem?> GetByIdAsync(Guid inventoryId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_items.FirstOrDefault(item => item.Id == id && item.InventoryId == inventoryId));

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

    public Task DeleteAsync(Guid inventoryId, Guid id, CancellationToken cancellationToken)
    {
        _items.RemoveAll(item => item.Id == id && item.InventoryId == inventoryId);
        return Task.CompletedTask;
    }

    public Task DeleteAllInInventoryAsync(Guid inventoryId, CancellationToken cancellationToken)
    {
        _items.RemoveAll(item => item.InventoryId == inventoryId);
        return Task.CompletedTask;
    }
}
