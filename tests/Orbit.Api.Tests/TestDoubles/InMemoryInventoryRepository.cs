using Orbit.Core.Inventory;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IInventoryRepository"/> stub for unit tests that need real add/lookup/update
/// behavior, including per-user ownership scoping, without spinning up Postgres.
/// </summary>
internal sealed class InMemoryInventoryRepository : IInventoryRepository
{
    private readonly List<InventoryItem> _items = [];

    public Task<IReadOnlyList<InventoryItem>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<InventoryItem>>(_items.Where(item => item.UserId == userId).ToList());

    public Task<InventoryItem?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_items.FirstOrDefault(item => item.Id == id && item.UserId == userId));

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

    public Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        _items.RemoveAll(item => item.Id == id && item.UserId == userId);
        return Task.CompletedTask;
    }
}
