using Orbit.Core.Tasks;

namespace Orbit.Core.Inventories;

/// <summary>
/// One entry that asks a shelf item for something: which list it is on, what the line says, and how
/// much of it it wants. What <see cref="InventoryItem.Usage"/> is the sum of, kept apart so a reader
/// can be told <b>which</b> lists are asking rather than only how much they add up to.
/// </summary>
/// <param name="Quantity">
/// <see cref="TaskItem.RequiredQuantity"/>, read as one where the entry says nothing - the same rule
/// ShelfUsage counts by, so a share and a count never disagree.
/// </param>
public sealed record ShelfClaim(
    Guid ShelfItemId, Guid TaskListId, string TaskListName, Guid TaskItemId, string Description, decimal Quantity);

/// <summary>
/// The other direction of ShelfUsage. That one takes what the lists ask for and writes it onto the
/// shelf; this takes an amount somebody settled on the shelf and writes it back onto the lists, so the
/// two are one number rather than two that drift.
///
/// **Why it cannot simply write.** A shelf item may be asked for by several entries at once - two
/// recipes wanting the same flour - and "the flour is kept at six" says nothing about how six divides
/// between them. So:
///
/// - nothing asks for it: there is nothing to write, and the shelf keeps its own minimum;
/// - exactly one entry asks: that entry is the whole of the demand, and it is given the new amount;
/// - several ask: nothing is written unless the reader said to split it evenly, because any other
///   division is a guess about which recipe got bigger. The reader is warned, named the lists, and can
///   go and change the one that actually moved - see InventoryEditor.
/// </summary>
public sealed class ShelfDemand(ITaskRepository taskRepository, ManagedRestockLists managedRestockLists)
{
    /// <summary>
    /// Which entries ask for each of <paramref name="shelfItemIds"/>, across every list this reader
    /// owns. Private lists are left out for the reason ShelfUsage leaves them out: the server cannot
    /// read what is on one, so it cannot honestly say whether it asks for anything. Orbit's own restock
    /// lists are left out too - see ManagedRestockLists.
    /// </summary>
    public async Task<IReadOnlyList<ShelfClaim>> WhoAsksForAsync(
        Guid ownerId, IReadOnlySet<Guid> shelfItemIds, CancellationToken cancellationToken)
    {
        if (shelfItemIds.Count == 0)
        {
            return [];
        }

        return await ClaimsOnAsync(
            await taskRepository.GetAllAsync(ownerId, updatedSinceUtc: null, cancellationToken), shelfItemIds,
            cancellationToken);
    }

    /// <summary>
    /// Writes <paramref name="wanted"/> - shelf item to the amount it is now kept at - back onto the
    /// entries asking for it, by the rule in this class's summary. <paramref name="splitEvenly"/> names
    /// the shelf items whose reader answered "split it evenly" to the warning; every other shared one is
    /// left exactly as it was.
    ///
    /// Answers which shelf items it actually wrote something for, so the caller counts usage again for
    /// those and only those - see ShelfUsage, which has to run afterwards or the shelf would still be
    /// holding the old total.
    /// </summary>
    public async Task<IReadOnlySet<Guid>> WriteBackAsync(
        Guid ownerId, IReadOnlyDictionary<Guid, decimal> wanted, IReadOnlySet<Guid> splitEvenly,
        CancellationToken cancellationToken)
    {
        if (wanted.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var taskLists = await taskRepository.GetAllAsync(ownerId, updatedSinceUtc: null, cancellationToken);
        var claims = await ClaimsOnAsync(taskLists, wanted.Keys.ToHashSet(), cancellationToken);

        var written = new HashSet<Guid>();
        // Entry id to the amount it is to ask for. Gathered across every shelf item first, because one
        // list may hold entries for several of them and is written once either way.
        var byEntryId = new Dictionary<Guid, decimal>();
        foreach (var (shelfItemId, amount) in wanted)
        {
            var asking = claims.Where(claim => claim.ShelfItemId == shelfItemId).ToList();
            if (asking.Count == 0 || (asking.Count > 1 && !splitEvenly.Contains(shelfItemId)))
            {
                continue;
            }

            // A shelf is never kept at less than nothing, and neither is what a list asks of it.
            var shares = SharesOf(Math.Max(amount, 0m), asking.Count);
            foreach (var (claim, share) in asking.Zip(shares))
            {
                byEntryId[claim.TaskItemId] = share;
            }

            written.Add(shelfItemId);
        }

        if (byEntryId.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var changedLists = taskLists.Where(taskList => taskList.AskFor(byEntryId)).ToList();
        if (changedLists.Count > 0)
        {
            await taskRepository.UpdateManyAsync(changedLists, cancellationToken);
        }

        return written;
    }

    private async Task<IReadOnlyList<ShelfClaim>> ClaimsOnAsync(
        IReadOnlyList<TaskList> taskLists, IReadOnlySet<Guid> shelfItemIds, CancellationToken cancellationToken)
    {
        var asking = taskLists
            .Where(taskList => !taskList.IsPrivate
                && taskList.Items.Any(item => item.LinkedInventoryItemId is { } linked && shelfItemIds.Contains(linked)))
            .ToList();

        var orbitsOwn = await managedRestockLists.AmongAsync(asking.Select(taskList => taskList.Id), cancellationToken);
        return
        [
            .. asking
                .Where(taskList => !orbitsOwn.Contains(taskList.Id))
                .SelectMany(taskList => taskList.Items
                    .Where(item => item.LinkedInventoryItemId is { } linked && shelfItemIds.Contains(linked))
                    .Select(item => new ShelfClaim(
                        item.LinkedInventoryItemId!.Value, taskList.Id, taskList.Title, item.Id, item.Description,
                        item.RequiredQuantity ?? 1)))
        ];
    }

    /// <summary>
    /// <paramref name="amount"/> divided into <paramref name="ways"/> equal parts, to the two decimal
    /// places the amount fields are typed in. Rounding every share leaves the total a little off - five
    /// three ways is 1.67 three times, which is 5.01 - so the first share carries the difference and
    /// what the lists ask for adds up to exactly what was typed on the shelf.
    /// </summary>
    private static IReadOnlyList<decimal> SharesOf(decimal amount, int ways)
    {
        var each = Math.Round(amount / ways, 2, MidpointRounding.AwayFromZero);
        var shares = Enumerable.Repeat(each, ways).ToArray();
        shares[0] += amount - (each * ways);
        return shares;
    }
}
