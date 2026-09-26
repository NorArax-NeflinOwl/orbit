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
///
/// <b>A row whose use-by date has passed crosses the entry out rather than off</b> - asked for on
/// 2026-09-24, and written without anybody having to say why. Holding four of something is not the same
/// as holding four of it that are any good, and until this the shelf answered the entry yes on the count
/// alone: a list asking for stock it already had, all of it months past its date, read as done. A cross
/// (<see cref="TaskItem.IsFailed"/>) says what is true of it - finished with, and not done - and it is
/// the shelf's to take back, so replacing the row ticks the entry off again and letting the count drop
/// reopens it.
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

        var (covered, expired) = await WhatTheShelvesSayAsync(ownerUserId, cancellationToken);
        var moved = false;
        foreach (var entry in standingForAShelf)
        {
            var shelfItemId = entry.LinkedInventoryItemId!.Value;
            // Expiry beats holding enough, and that is the point of it: a row can hold four of something
            // and hold nothing worth having. See InventoryItem.HasExpired.
            var hasExpired = expired.Contains(shelfItemId);
            var isCovered = !hasExpired && covered.Contains(shelfItemId);

            // Whose answer this entry is carrying. The shelf may change its own mind as often as the
            // count moves; somebody's own tick is theirs, and an entry that put its minimum on the shelf
            // (Stocked) is answering a different question again.
            var isTheShelfs = entry.Stock == TaskItemStock.CrossedOffByTheShelf;
            if (!isTheShelfs && entry.IsResolved)
            {
                continue;
            }

            if (hasExpired)
            {
                if (!entry.IsFailed)
                {
                    entry.GiveUp();
                    // Asked rather than assumed, the way the tick below is: a linked entry is answered by
                    // the lists it stands for, and nothing here overrules them.
                    if (entry.IsFailed && entry.Stock != TaskItemStock.Stocked)
                    {
                        entry.RecordStock(TaskItemStock.CrossedOffByTheShelf);
                        moved = true;
                    }
                }

                continue;
            }

            if (isCovered)
            {
                entry.Complete();
                // Marked as the shelf's doing only where it actually took, and only where nothing of the
                // entry's own is on the shelf - a ticked entry that put its minimum there keeps saying so.
                if (entry.IsCompleted && entry.Stock == TaskItemStock.None)
                {
                    entry.RecordStock(TaskItemStock.CrossedOffByTheShelf);
                }

                moved |= entry.IsCompleted;
                continue;
            }

            // And back again: what the shelf settled, the shelf unsettles once it no longer holds enough -
            // or once the row that had expired has been replaced by one that has not. Nothing else is
            // touched - see the note at the top of this class about whose tick is whose.
            if (isTheShelfs)
            {
                entry.Reopen();
                entry.RecordStock(TaskItemStock.None);
                moved = true;
            }
        }

        return moved;
    }

    /// <summary>
    /// Every one of this reader's shelf rows that is asking for nothing, and every one whose use-by date
    /// has passed - by id, in one pass, because both answers come from the same rows and reading them
    /// twice would double what a save costs.
    ///
    /// All of their storages rather than the one this list is measured against: an entry can be moved to
    /// another list, and the row it points at then sits on a shelf that list has never been measured
    /// against.
    ///
    /// A row can be in both sets - it holds enough of something that has gone off - and the caller reads
    /// expiry first, which is what makes "there are four and none of them any good" answer the entry
    /// honestly.
    /// </summary>
    private async Task<(HashSet<Guid> Covered, HashSet<Guid> Expired)> WhatTheShelvesSayAsync(
        Guid ownerUserId, CancellationToken cancellationToken)
    {
        var covered = new HashSet<Guid>();
        var expired = new HashSet<Guid>();
        foreach (var inventory in
            await _inventoryRepository.GetAllAsync(ownerUserId, updatedSinceUtc: null, cancellationToken))
        {
            foreach (var shelfItem in await _inventoryItemRepository.GetAllAsync(inventory.Id, cancellationToken))
            {
                if (Covers(shelfItem))
                {
                    covered.Add(shelfItem.Id);
                }

                if (shelfItem.HasExpired)
                {
                    expired.Add(shelfItem.Id);
                }
            }
        }

        return (covered, expired);
    }
}
