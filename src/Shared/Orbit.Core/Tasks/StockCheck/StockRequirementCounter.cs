using Orbit.Core.Inventories;

namespace Orbit.Core.Tasks.StockCheck;

/// <summary>
/// Works out what a task list's work costs, and whether an inventory covers it.
///
/// The counting rule is that repetition is quantity: a list saying "Screw" three times needs three
/// screws. That is what makes a checklist a bill of materials without asking anyone to type a number
/// beside every line - the list is written the way the work is done, one line per thing to do.
///
/// Each line adds what it asks for rather than a flat one: an entry that says how little is too little
/// (TaskItemProduct.MinimumQuantity) adds that minimum, and one that says nothing adds one, which is the
/// rule above. Flour named by two recipes wanting two and three needs five, and a third recipe that only
/// names it makes six. Once entries stand for a shelf item, that item's minimum is what they handed
/// over when it was built, so it is counted once for all of them rather than again per line - which is
/// what lets the shelf, this check and the restock errands read one number.
///
/// This is the only place the rule lives: generating a shelf, the stock check and the shortfalls it
/// raises all ask here, so they cannot come to count the same list two ways.
/// </summary>
public static class StockRequirementCounter
{
    /// <summary>
    /// Counts what <paramref name="items"/> call for and measures it against <paramref name="stock"/>.
    ///
    /// Entries that only point at another list are not work and are skipped; so is anything not due yet.
    /// A line with a due date in the future is work that has not come round - counting it would report a
    /// shortfall for something nobody is about to start, and send a restock task out early.
    /// </summary>
    public static TaskListStockCheck Count(
        IEnumerable<TaskItem> items, IEnumerable<InventoryItem> stock, DateTimeOffset nowUtc,
        IReadOnlyDictionary<string, decimal>? alsoAskedFor = null)
        => Measure(items.Where(item => !IsNotDueYet(item, nowUtc)), stock, alsoAskedFor);

    /// <summary>
    /// What <paramref name="items"/> call for, by name. This is one list's half of a shelf several lists
    /// share - see <see cref="Count"/>'s alsoAskedFor. The shelf is passed only so an entry standing for
    /// one of its items is counted the way <see cref="Count"/> counts it; nothing is measured against it.
    ///
    /// <paramref name="shelfItemsAlreadyCounted"/> is shared across every list measured against the one
    /// shelf, and filled as it goes: a shelf item's minimum is what all of its entries handed over
    /// together, so it is asked for once however many lists point at it. Counted per list, two lists
    /// standing for one row each asked for its whole minimum and both were told they were short.
    /// </summary>
    public static IReadOnlyDictionary<string, decimal> DemandOf(
        IEnumerable<TaskItem> items, IEnumerable<InventoryItem> stock, DateTimeOffset nowUtc,
        ISet<Guid>? shelfItemsAlreadyCounted = null)
        => Measure(items.Where(item => !IsNotDueYet(item, nowUtc)), stock, alsoAskedFor: null, shelfItemsAlreadyCounted).Requirements
            .ToDictionary(requirement => Normalize(requirement.Name), requirement => requirement.Required);

    /// <summary>
    /// What the work calls for in total, whenever each piece of it falls due, against an empty shelf.
    /// This is the question a shelf being built has to answer - it holds what the whole job will need -
    /// while <see cref="Count"/> answers whether the job can be started today.
    /// </summary>
    public static TaskListStockCheck CountRegardlessOfDueDate(IEnumerable<TaskItem> items)
        => Measure(items, [], alsoAskedFor: null);

    private static TaskListStockCheck Measure(
        IEnumerable<TaskItem> items, IEnumerable<InventoryItem> stock,
        IReadOnlyDictionary<string, decimal>? alsoAskedFor, ISet<Guid>? shelfItemsAlreadyCounted = null)
    {
        var shelf = stock.ToList();
        var available = shelf
            .GroupBy(item => Normalize(item.Name))
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
        var minimumsByShelfItemId = shelf
            .Where(item => item.MinimumQuantity is not null)
            .ToDictionary(item => item.Id, item => item.MinimumQuantity!.Value);
        var shelfItemsCounted = shelfItemsAlreadyCounted ?? new HashSet<Guid>();

        var required = new Dictionary<string, decimal>();
        var done = new Dictionary<string, decimal>();
        var smallestAmountWritten = new Dictionary<string, decimal>();
        // The order things are first asked for, so the report reads like the list it came from.
        var namesInOrder = new List<string>();
        var displayNames = new Dictionary<string, string>();

        foreach (var item in items)
        {
            if (item.IsALinkToOtherLists)
            {
                continue;
            }

            var key = Normalize(item.Description);
            if (key.Length == 0)
            {
                continue;
            }

            var asksFor = RequiredBy(item, minimumsByShelfItemId, shelfItemsCounted);
            if (required.TryAdd(key, asksFor))
            {
                namesInOrder.Add(key);
                displayNames[key] = item.Description.Trim();
                done[key] = 0;
            }
            else
            {
                required[key] += asksFor;
            }

            // Crossed out counts as finished with: what a list still needs is what is still owed.
            if (item.IsResolved)
            {
                done[key] += 1;
            }

            // Zero is the box nobody filled in rather than a claim that there is none (see
            // TaskItemProduct.Quantity), so it takes no part: one untouched entry would otherwise pin
            // every shelf at nothing and overrule the amount somebody did write on another.
            if (item.Product is { Quantity: > 0 } product)
            {
                smallestAmountWritten[key] = smallestAmountWritten.TryGetValue(key, out var least)
                    ? Math.Min(least, product.Quantity)
                    : product.Quantity;
            }
        }

        return new TaskListStockCheck(
            [.. namesInOrder.Select(key => new StockRequirement(
                displayNames[key],
                required[key],
                ShareOfTheShelf(available.GetValueOrDefault(key), required[key], alsoAskedFor?.GetValueOrDefault(key) ?? 0),
                done[key],
                smallestAmountWritten.TryGetValue(key, out var least) ? least : null))]);
    }

    /// <summary>
    /// How much one entry adds to what its name calls for: its own minimum, or one where it says none.
    ///
    /// An entry standing for a shelf item has no minimum of its own any more - it handed it to that
    /// item, whose minimum is the sum of every entry it was built from (see
    /// GenerateInventoryFromTaskListCommandHandler, which asks this counter for it).
    /// So the item's minimum is added once, by the first entry pointing at it, and the rest add nothing:
    /// counting it again per line would ask for five twice. An item with no minimum was left to the
    /// counting rule, and its entries are counted one by one as they always were.
    /// </summary>
    private static decimal RequiredBy(
        TaskItem item, IReadOnlyDictionary<Guid, decimal> minimumsByShelfItemId, ISet<Guid> shelfItemsCounted)
    {
        if (item.LinkedInventoryItemId is { } shelfItemId
            && minimumsByShelfItemId.TryGetValue(shelfItemId, out var minimum))
        {
            return shelfItemsCounted.Add(shelfItemId) ? minimum : 0;
        }

        return item.Product?.MinimumQuantity ?? 1;
    }

    /// <summary>
    /// How much of what is on the shelf is this list's, when other lists are measured against the same
    /// inventory. Split in proportion to what each asks for: a shelf holding one of something that two
    /// lists each want two of leaves both of them short by one and a half, rather than telling each of
    /// them the whole one is theirs. With nothing else asking, this is simply what is on the shelf.
    /// </summary>
    private static decimal ShareOfTheShelf(decimal onTheShelf, decimal required, decimal askedForElsewhere)
    {
        if (askedForElsewhere <= 0 || required <= 0)
        {
            return onTheShelf;
        }

        return onTheShelf * required / (required + askedForElsewhere);
    }

    /// <summary>
    /// A due date still ahead means the work is not on yet. An entry with no due date at all is work
    /// waiting to be done now, so it counts.
    /// </summary>
    private static bool IsNotDueYet(TaskItem item, DateTimeOffset nowUtc)
        => item.DueDateUtc is { } dueDate && dueDate > nowUtc;

    /// <summary>
    /// What counts as the same thing: trimmed, and compared without case. "screw", "Screw" and " Screw "
    /// are one entry in an inventory and should be one line here.
    /// </summary>
    private static string Normalize(string name) => name.Trim().ToLowerInvariant();
}
