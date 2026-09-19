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
/// <b>What the shelf crossed off, the shelf can reopen</b> - asked for on 2026-09-19: counting a product
/// down past what the lists need should put the work back in front of the reader, the same way counting
/// it up took it away. Only its own, though: an entry crossed off here is marked as such
/// (<see cref="TaskItemStock.CrossedOffByTheShelf"/>), and a tick somebody put there by hand is theirs -
/// taking it away because a count moved would be arguing with them, and on a restock list a crossed-off
/// errand means "I have been", which is what <see cref="RestockCompletion"/> reads to fill the shelf, so
/// unticking one would undo the trip.
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
        => shelfItem.EffectiveMinimum is not null && !shelfItem.BelongsOnTheRestockList;

    /// <summary>
    /// Settles every entry in <paramref name="items"/> against the shelf row it stands for: crosses off
    /// what the shelf covers, reopens what it has stopped covering and had crossed off itself, and
    /// answers whether anything moved. The entries are changed where they are, so the caller writes
    /// them with the save it was already making rather than making a second one.
    ///
    /// Reads nothing at all for a list with no entry standing for a shelf row, which is nearly every
    /// list - this runs on every save, and a save of an ordinary list must not pay for a shelf it has
    /// not got.
    /// </summary>
    public async Task<bool> SettleWhatTheShelfSaysAsync(
        Guid ownerUserId, IReadOnlyList<TaskItem> items, CancellationToken cancellationToken)
    {
        var standingForAShelf = items
            .Where(item => item.Kind == TaskItemKind.Inventory && item.LinkedInventoryItemId is not null)
            .ToList();
        if (standingForAShelf.Count == 0)
        {
            return false;
        }

        var covered = await CoveredShelfItemIdsAsync(ownerUserId, cancellationToken);
        var moved = false;
        foreach (var entry in standingForAShelf)
        {
            var isCovered = covered.Contains(entry.LinkedInventoryItemId!.Value);
            if (isCovered && !entry.IsResolved)
            {
                entry.Complete();
                // Asked rather than assumed: an entry standing for other lists is completed by them -
                // see TaskItem.Complete. Marked as the shelf's doing only where it actually took, and
                // only where nothing of the entry's own is on the shelf - a ticked entry that put its
                // minimum there keeps saying so.
                if (entry.IsCompleted && entry.Stock == TaskItemStock.None)
                {
                    entry.RecordStock(TaskItemStock.CrossedOffByTheShelf);
                }

                moved |= entry.IsCompleted;
                continue;
            }

            // And back again: what the shelf crossed off, the shelf reopens once it no longer holds
            // enough. Nothing else is touched - see the note at the top of this class about whose tick
            // is whose.
            if (!isCovered && entry.Stock == TaskItemStock.CrossedOffByTheShelf)
            {
                entry.Reopen();
                entry.RecordStock(TaskItemStock.None);
                moved = true;
            }
        }

        return moved;
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
