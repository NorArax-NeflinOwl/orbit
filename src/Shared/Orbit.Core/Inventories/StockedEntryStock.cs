using Orbit.Core.Tasks;

namespace Orbit.Core.Inventories;

/// <summary>
/// What ticking a list's product entry does to the shelf behind it, and what unticking one takes back.
///
/// The rule, as the user gave it on 2026-09-19: a product entry's minimum is how much of that product
/// this piece of work needs. <b>Ticking the entry puts that much on the shelf</b> - the tick is somebody
/// saying they went and got it. <b>Unticking takes the same amount back off</b>, because they did not.
/// <b>Crossing the entry off does neither</b>: giving up on something after having got it does not
/// unbuy it, and giving up on something never got puts nothing anywhere.
///
/// The amount is the entry's own (Orbit.Core.Tasks.TaskItem.RequiredQuantity, one where it says
/// nothing), never the shelf item's minimum. Those differ the moment two lists ask for the same
/// product: the shelf is kept at what they add up to (see <see cref="InventoryItem.Usage"/>), and
/// ticking one of them is a claim about that one only.
///
/// Whether an entry's amount is on the shelf right now is stored on the entry rather than worked out
/// from its tick (see <see cref="TaskItemStock"/>): the same save runs this again and again - every
/// write of the list goes through it - and a rule that could not tell "already counted" from "just
/// ticked" would add the amount on every save.
/// </summary>
public sealed class StockedEntryStock
{
    private readonly IInventoryItemRepository _inventoryItemRepository;

    public StockedEntryStock(IInventoryItemRepository inventoryItemRepository)
        => _inventoryItemRepository = inventoryItemRepository;

    /// <summary>
    /// Settles every entry in <paramref name="items"/> against the shelf row it stands for: puts on what
    /// a fresh tick owes, takes off what a fresh untick takes back, and leaves everything else exactly
    /// as it is. The entries are changed where they stand, so the caller writes them with the save it
    /// was already making.
    ///
    /// Reads nothing at all for a list with no entry standing for a shelf row, which is nearly every
    /// list - this runs on every save, and a save of an ordinary list must not pay for a shelf it has
    /// not got.
    /// </summary>
    public async Task SettleAsync(IReadOnlyList<TaskItem> items, CancellationToken cancellationToken)
    {
        var standingForAShelf = items
            .Where(item => item.LinkedInventoryItemId is not null && item.Stock != WhereItShouldBe(item))
            .ToList();
        if (standingForAShelf.Count == 0)
        {
            return;
        }

        var shelfItems = (await _inventoryItemRepository.GetByIdsAsync(
                [.. standingForAShelf.Select(item => item.LinkedInventoryItemId!.Value).Distinct()],
                cancellationToken))
            .ToDictionary(shelfItem => shelfItem.Id);

        foreach (var entry in standingForAShelf)
        {
            var wanted = WhereItShouldBe(entry);
            if (!shelfItems.TryGetValue(entry.LinkedInventoryItemId!.Value, out var shelfItem))
            {
                // The row is gone from every shelf this reader has. Nothing to move, and the entry is
                // told so rather than left owing an amount to a shelf that does not exist.
                entry.RecordStock(TaskItemStock.None);
                continue;
            }

            // One direction or the other, never both: the entry was either not counted and now is, or
            // was and now is not - see TaskItemStock, where the three states are. A reopened entry the
            // shelf had crossed off is the third case, and it moves nothing: nothing of it was ever on
            // the shelf.
            var moved = wanted switch
            {
                TaskItemStock.Stocked => shelfItem.MoveStockBy(AmountOf(entry)),
                _ when entry.Stock == TaskItemStock.Stocked => shelfItem.MoveStockBy(-AmountOf(entry)),
                _ => false
            };

            entry.RecordStock(wanted);
            if (moved)
            {
                await _inventoryItemRepository.UpdateAsync(shelfItem, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Where an entry stands with the shelf once its tick has been read. An entry the shelf itself
    /// crossed off stays that way while it is still ticked - that is what the reopening rule reads to
    /// tell it from a tick somebody gave it by hand (see StockedEntryCompletion) - and goes back to
    /// owing the shelf nothing the moment anybody reopens it.
    /// </summary>
    private static TaskItemStock WhereItShouldBe(TaskItem entry)
        => entry.BelongsOnTheShelf ? TaskItemStock.Stocked
            : entry.Stock == TaskItemStock.CrossedOffByTheShelf && entry.IsCompleted
                ? TaskItemStock.CrossedOffByTheShelf
                : TaskItemStock.None;

    /// <summary>
    /// How much this entry is worth to the shelf: its own minimum, or one where it says nothing - the
    /// counting rule the rest of this reads it by (see ShelfUsage, which adds the same numbers up).
    /// </summary>
    private static decimal AmountOf(TaskItem entry) => entry.RequiredQuantity ?? 1;
}
