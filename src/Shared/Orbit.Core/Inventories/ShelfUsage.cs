using Orbit.Core.Tasks;

namespace Orbit.Core.Inventories;

/// <summary>
/// Keeps each shelf item's <see cref="InventoryItem.Usage"/> - how much of it the owner's task lists ask
/// for, every entry that stands for it adding its own minimum (Orbit.Core.Tasks.TaskItem.RequiredQuantity,
/// one when it says nothing). The shelf's minimum is never read as lower than this, so the shelf holds at
/// least what the plans need.
///
/// A count kept rather than worked out on every read: everything that restocks reads the minimum off the
/// item, in many places, and a stored number is what lets all of them keep doing so. It is recounted from
/// scratch for the items a save may have moved - never nudged up or down - so a count cannot drift.
///
/// Every entry counts, ticked or not, on every one of the owner's lists that the server can read: the
/// user's rule is that a product is needed as many times as it is written down. Private lists hold no
/// entries on the server, so theirs cannot be counted.
/// </summary>
public sealed class ShelfUsage(ITaskRepository taskRepository, IInventoryItemRepository inventoryItemRepository)
{
    /// <summary>The shelf items these lists' entries stand for - what a save of them may change the count of.</summary>
    public static IReadOnlySet<Guid> ShelfItemsOf(IEnumerable<TaskList> taskLists)
        => taskLists
            .SelectMany(taskList => taskList.Items)
            .Select(item => item.LinkedInventoryItemId)
            .OfType<Guid>()
            .ToHashSet();

    /// <summary>Counts again what every one of <paramref name="shelfItemIds"/> is asked for, and writes the ones that moved.</summary>
    public async Task RecountAsync(Guid ownerId, IReadOnlySet<Guid> shelfItemIds, CancellationToken cancellationToken)
    {
        if (shelfItemIds.Count == 0)
        {
            return;
        }

        var entries = (await taskRepository.GetAllAsync(ownerId, updatedSinceUtc: null, cancellationToken))
            .Where(taskList => !taskList.IsPrivate)
            .SelectMany(taskList => taskList.Items)
            .Where(item => item.LinkedInventoryItemId is { } linked && shelfItemIds.Contains(linked))
            .ToList();

        foreach (var shelfItem in await inventoryItemRepository.GetByIdsAsync(shelfItemIds, cancellationToken))
        {
            var usage = entries
                .Where(entry => entry.LinkedInventoryItemId == shelfItem.Id)
                .Sum(entry => entry.RequiredQuantity ?? 1);
            if (shelfItem.CountUsage(usage))
            {
                await inventoryItemRepository.UpdateAsync(shelfItem, cancellationToken);
            }
        }
    }
}
