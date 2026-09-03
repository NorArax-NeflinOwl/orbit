using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;

namespace Orbit.Core.Tasks.GetInventoryReferences;

public sealed class GetInventoryReferencesQueryHandler
    : IRequestHandler<GetInventoryReferencesQuery, IReadOnlyList<InventoryReference>>
{
    private readonly TaskListAccessResolver _taskListAccessResolver;
    private readonly ITaskRepository _taskRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;

    public GetInventoryReferencesQueryHandler(
        TaskListAccessResolver taskListAccessResolver, ITaskRepository taskRepository,
        IInventoryRepository inventoryRepository, IInventoryItemRepository inventoryItemRepository)
    {
        _taskListAccessResolver = taskListAccessResolver;
        _taskRepository = taskRepository;
        _inventoryRepository = inventoryRepository;
        _inventoryItemRepository = inventoryItemRepository;
    }

    public async Task<IReadOnlyList<InventoryReference>> HandleAsync(
        GetInventoryReferencesQuery request, CancellationToken cancellationToken)
    {
        var taskList = await _taskListAccessResolver.ResolveAsync(request.UserId, request.TaskListId, cancellationToken);
        if (taskList is null)
        {
            return [];
        }

        var errands = taskList.Items
            .Where(item => item.Kind == TaskItemKind.Inventory && item.LinkedInventoryItemId is not null)
            .ToList();
        if (errands.Count == 0)
        {
            return [];
        }

        // The reader's own inventories, read once. A shelf item carries its inventory id but not the
        // inventory, and the screen needs the name to say which shelf it is talking about.
        var shelvesByItemId = await ShelvesByItemIdAsync(request.UserId, cancellationToken);
        var elsewhere = await ErrandsElsewhereAsync(request.UserId, request.TaskListId, cancellationToken);

        var references = new List<InventoryReference>(errands.Count);
        foreach (var errand in errands)
        {
            var inventoryItemId = errand.LinkedInventoryItemId!.Value;
            if (!shelvesByItemId.TryGetValue(inventoryItemId, out var shelf))
            {
                // The product, or the whole inventory, is gone or was never the reader's. The entry is
                // still readable as a line of text; it just has nothing to link to.
                continue;
            }

            references.Add(new InventoryReference(
                errand.Id, inventoryItemId, shelf.ItemName, shelf.InventoryId, shelf.InventoryName,
                elsewhere.GetValueOrDefault(inventoryItemId, [])));
        }

        return references;
    }

    /// <summary>Every shelf item the reader owns, with the inventory it sits in, keyed by the item's id.</summary>
    private async Task<Dictionary<Guid, ShelfLocation>> ShelvesByItemIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var byItemId = new Dictionary<Guid, ShelfLocation>();
        foreach (var inventory in await _inventoryRepository.GetAllAsync(userId, updatedSinceUtc: null, cancellationToken))
        {
            foreach (var item in await _inventoryItemRepository.GetAllAsync(inventory.Id, cancellationToken))
            {
                byItemId[item.Id] = new ShelfLocation(inventory.Id, inventory.Name, item.Name);
            }
        }

        return byItemId;
    }

    /// <summary>
    /// Which of the reader's other lists carry an errand about the same shelf item, keyed by that item.
    /// Read across every list rather than only the managed ones: an errand can be put on an ordinary
    /// list by hand, and the point of showing this is that the reader can be looking at one of two
    /// places asking for the same thing.
    /// </summary>
    private async Task<Dictionary<Guid, IReadOnlyList<InventoryReferenceElsewhere>>> ErrandsElsewhereAsync(
        Guid userId, Guid excludedTaskListId, CancellationToken cancellationToken)
    {
        var byInventoryItemId = new Dictionary<Guid, List<InventoryReferenceElsewhere>>();
        foreach (var other in await _taskRepository.GetAllAsync(userId, updatedSinceUtc: null, cancellationToken))
        {
            if (other.Id == excludedTaskListId)
            {
                continue;
            }

            foreach (var item in other.Items)
            {
                if (item.Kind != TaskItemKind.Inventory || item.LinkedInventoryItemId is not { } inventoryItemId)
                {
                    continue;
                }

                if (!byInventoryItemId.TryGetValue(inventoryItemId, out var found))
                {
                    found = [];
                    byInventoryItemId[inventoryItemId] = found;
                }

                found.Add(new InventoryReferenceElsewhere(other.Id, other.Title, item.Id));
            }
        }

        return byInventoryItemId.ToDictionary(
            entry => entry.Key, entry => (IReadOnlyList<InventoryReferenceElsewhere>)entry.Value);
    }

    /// <summary>Where one shelf item sits, as the screen needs to say it.</summary>
    private sealed record ShelfLocation(Guid InventoryId, string InventoryName, string ItemName);
}
