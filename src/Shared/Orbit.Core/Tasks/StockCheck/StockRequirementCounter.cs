using Orbit.Core.Inventory;

namespace Orbit.Core.Tasks.StockCheck;

/// <summary>
/// Works out what a task list's work costs, and whether a warehouse covers it.
///
/// The counting rule is that repetition is quantity: a list saying "Screw" three times needs three
/// screws. That is what makes a checklist a bill of materials without asking anyone to type a number
/// beside every line - the list is written the way the work is done, one line per thing to do.
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
    /// What <paramref name="items"/> call for, by name, with nothing measured against it. This is one
    /// list's half of a shelf several lists share - see <see cref="Count"/>'s alsoAskedFor.
    /// </summary>
    public static IReadOnlyDictionary<string, decimal> DemandOf(
        IEnumerable<TaskItem> items, DateTimeOffset nowUtc)
        => Measure(items.Where(item => !IsNotDueYet(item, nowUtc)), [], alsoAskedFor: null).Requirements
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
        IReadOnlyDictionary<string, decimal>? alsoAskedFor)
    {
        var available = stock
            .GroupBy(item => Normalize(item.Name))
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        var required = new Dictionary<string, decimal>();
        var done = new Dictionary<string, decimal>();
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

            if (required.TryAdd(key, 1))
            {
                namesInOrder.Add(key);
                displayNames[key] = item.Description.Trim();
                done[key] = 0;
            }
            else
            {
                required[key] += 1;
            }

            if (item.IsCompleted)
            {
                done[key] += 1;
            }
        }

        return new TaskListStockCheck(
            [.. namesInOrder.Select(key => new StockRequirement(
                displayNames[key],
                required[key],
                ShareOfTheShelf(available.GetValueOrDefault(key), required[key], alsoAskedFor?.GetValueOrDefault(key) ?? 0),
                done[key]))]);
    }

    /// <summary>
    /// How much of what is on the shelf is this list's, when other lists are measured against the same
    /// warehouse. Split in proportion to what each asks for: a shelf holding one of something that two
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
    /// are one entry in a warehouse and should be one line here.
    /// </summary>
    private static string Normalize(string name) => name.Trim().ToLowerInvariant();
}
