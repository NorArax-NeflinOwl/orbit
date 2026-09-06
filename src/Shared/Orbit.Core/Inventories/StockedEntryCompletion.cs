using Orbit.Core.Tasks;

namespace Orbit.Core.Inventories;

/// <summary>
/// Crosses off the entries a shelf has already answered.
///
/// An inventory entry names a thing the work needs and says how much of it to keep. Once it stands for a
/// row on a shelf, that row is what knows whether the answer is yes - and a list that goes on asking for
/// something while four of it sit on the shelf is a list that stops being read. So the tick is taken
/// from the shelf rather than waited for from a finger.
///
/// Only ever crosses off, never back. A tick somebody put there by hand is theirs, and taking it away
/// because a count moved would be arguing with them; and on a restock list a crossed-off errand means "I
/// have been", which is what <see cref="RestockCompletion"/> reads to fill the shelf, so unticking one
/// would undo the trip.
///
/// Two shelf rows are left alone whatever their count says, because neither has an amount that settles
/// the question: one with no minimum at all - the "leave the minimum empty to have it counted instead"
/// case - and one marked to be looked at every round, where crossing off answers "have you looked". See
/// <see cref="InventoryItem.BelongsOnTheRestockList"/>.
/// </summary>
public sealed class StockedEntryCompletion
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;

    public StockedEntryCompletion(
        IInventoryRepository inventoryRepository, IInventoryItemRepository inventoryItemRepository)
    {
        _inventoryRepository = inventoryRepository;
        _inventoryItemRepository = inventoryItemRepository;
    }

    /// <summary>
    /// Whether a shelf row holds what the entry standing for it asked to keep. The one rule, in one
    /// place, so the save and the storage being generated cannot come to different answers about the
    /// same row - see GenerateInventoryFromTaskListCommandHandler, which knows its rows already and so
    /// asks this directly rather than reading them back.
    /// </summary>
    public static bool Covers(InventoryItem shelfItem)
        => shelfItem.MinimumQuantity is not null && !shelfItem.BelongsOnTheRestockList;

    /// <summary>
    /// Crosses off every entry in <paramref name="items"/> whose shelf row covers it, and answers
    /// whether anything moved. The entries are changed where they are, so the caller writes them with
    /// the save it was already making rather than making a second one.
    ///
    /// Reads nothing at all for a list with no outstanding inventory entry, which is nearly every list -
    /// this runs on every save, and a save of an ordinary list must not pay for a shelf it has not got.
    /// </summary>
    public async Task<bool> CrossOffWhatTheShelfCoversAsync(
        Guid ownerUserId, IReadOnlyList<TaskItem> items, CancellationToken cancellationToken)
    {
        var waiting = items
            .Where(item =>
                item.Kind == TaskItemKind.Inventory && !item.IsCompleted && item.LinkedInventoryItemId is not null)
            .ToList();
        if (waiting.Count == 0)
        {
            return false;
        }

        var covered = await CoveredShelfItemIdsAsync(ownerUserId, cancellationToken);
        var crossedOff = false;
        foreach (var entry in waiting.Where(entry => covered.Contains(entry.LinkedInventoryItemId!.Value)))
        {
            entry.Complete();
            // Asked rather than assumed: an entry standing for other lists is completed by them - see
            // TaskItem.Complete.
            crossedOff |= entry.IsCompleted;
        }

        return crossedOff;
    }

    /// <summary>
    /// Every one of this reader's shelf rows that is asking for nothing, by id. All of their storages
    /// rather than the one this list is measured against: an entry can be moved to another list, and the
    /// row it points at then sits on a shelf that list has never been measured against.
    /// </summary>
    private async Task<HashSet<Guid>> CoveredShelfItemIdsAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        var covered = new HashSet<Guid>();
        foreach (var inventory in
            await _inventoryRepository.GetAllAsync(ownerUserId, updatedSinceUtc: null, cancellationToken))
        {
            foreach (var shelfItem in await _inventoryItemRepository.GetAllAsync(inventory.Id, cancellationToken))
            {
                if (Covers(shelfItem))
                {
                    covered.Add(shelfItem.Id);
                }
            }
        }

        return covered;
    }
}
