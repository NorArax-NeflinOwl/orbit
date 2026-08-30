using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;

namespace Orbit.Core.Tasks.GetInventoryReferences;

public sealed class GetInventoryReferencesQueryHandler
    : IRequestHandler<GetInventoryReferencesQuery, IReadOnlyList<InventoryReference>>
{
    private readonly TaskListAccessResolver _taskListAccessResolver;
    private readonly ITaskRepository _taskRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IInventoryRepository _inventoryRepository;

    public GetInventoryReferencesQueryHandler(
        TaskListAccessResolver taskListAccessResolver, ITaskRepository taskRepository,
        IWarehouseRepository warehouseRepository, IInventoryRepository inventoryRepository)
    {
        _taskListAccessResolver = taskListAccessResolver;
        _taskRepository = taskRepository;
        _warehouseRepository = warehouseRepository;
        _inventoryRepository = inventoryRepository;
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

        // The reader's own warehouses, read once. A shelf item carries its warehouse id but not the
        // warehouse, and the screen needs the name to say which shelf it is talking about.
        var shelvesByItemId = await ShelvesByItemIdAsync(request.UserId, cancellationToken);
        var elsewhere = await ErrandsElsewhereAsync(request.UserId, request.TaskListId, cancellationToken);

        var references = new List<InventoryReference>(errands.Count);
        foreach (var errand in errands)
        {
            var inventoryItemId = errand.LinkedInventoryItemId!.Value;
            if (!shelvesByItemId.TryGetValue(inventoryItemId, out var shelf))
            {
                // The product, or the whole warehouse, is gone or was never the reader's. The entry is
                // still readable as a line of text; it just has nothing to link to.
                continue;
            }

            references.Add(new InventoryReference(
                errand.Id, inventoryItemId, shelf.ItemName, shelf.WarehouseId, shelf.WarehouseName,
                elsewhere.GetValueOrDefault(inventoryItemId, [])));
        }

        return references;
    }

    /// <summary>Every shelf item the reader owns, with the warehouse it sits in, keyed by the item's id.</summary>
    private async Task<Dictionary<Guid, ShelfLocation>> ShelvesByItemIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var byItemId = new Dictionary<Guid, ShelfLocation>();
        foreach (var warehouse in await _warehouseRepository.GetAllAsync(userId, updatedSinceUtc: null, cancellationToken))
        {
            foreach (var item in await _inventoryRepository.GetAllAsync(warehouse.Id, cancellationToken))
            {
                byItemId[item.Id] = new ShelfLocation(warehouse.Id, warehouse.Name, item.Name);
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
    private sealed record ShelfLocation(Guid WarehouseId, string WarehouseName, string ItemName);
}
