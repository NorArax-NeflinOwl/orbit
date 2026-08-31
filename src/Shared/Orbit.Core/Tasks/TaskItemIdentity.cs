namespace Orbit.Core.Tasks;

/// <summary>What a save had to change so that no two entries claim the same id.</summary>
/// <param name="Items">The entries to store, with any contested id replaced.</param>
/// <param name="ListsToSaveToo">
/// Other lists whose entries were re-issued, which the caller has to persist alongside its own - see
/// <see cref="ITaskRepository.UpdateManyAsync"/>.
/// </param>
public sealed record TaskItemIdentityOutcome(
    IReadOnlyList<TaskItem> Items, IReadOnlyList<TaskList> ListsToSaveToo);

/// <summary>
/// Keeps entry ids unique now that clients mint them.
///
/// Clients name their own entries so that one written with no connection has an identity from the
/// moment it exists rather than being renamed by its first successful push - which is what let a phone
/// tie an appointment, a shelf item or anything else to an entry before the server had ever seen it.
/// The cost is that two clients can hand over the same id.
///
/// When they do, <b>both</b> entries are given fresh ones. Not "first wins": the server cannot tell
/// which of the two the reader meant, and keeping either id would silently make one entry stand for the
/// other's history - a due date, a reminder, a restock link. Renaming both is the only answer that
/// cannot quietly attach one entry's past to another, and it costs only the ids themselves, which
/// nothing outside this account refers to.
///
/// A collision is a client fault or a replayed payload rather than chance - two v4 GUIDs do not meet -
/// so this is a guard, not a routine path.
/// </summary>
public static class TaskItemIdentity
{
    /// <summary>
    /// <paramref name="heldElsewhere"/> is every other list of this owner's holding one of the incoming
    /// ids - see <see cref="ITaskRepository.GetHoldingItemsAsync"/>.
    /// </summary>
    public static TaskItemIdentityOutcome Resolve(
        IReadOnlyList<TaskItem> incoming, IReadOnlyList<TaskList> heldElsewhere)
    {
        var contested = ContestedIds(incoming, heldElsewhere);
        if (contested.Count == 0)
        {
            return new(incoming, []);
        }

        var changedLists = new List<TaskList>();
        foreach (var taskList in heldElsewhere)
        {
            if (taskList.ReissueItemIds(contested))
            {
                changedLists.Add(taskList);
            }
        }

        return new([.. incoming.Select(item => contested.Contains(item.Id) ? item.WithNewId() : item)], changedLists);
    }

    /// <summary>
    /// Every id that is claimed twice - either by two of the incoming entries, or by an incoming entry
    /// and one already living on another list.
    /// </summary>
    private static HashSet<Guid> ContestedIds(IReadOnlyList<TaskItem> incoming, IReadOnlyList<TaskList> heldElsewhere)
    {
        var seen = new HashSet<Guid>();
        var contested = new HashSet<Guid>();
        foreach (var item in incoming.Where(item => item.Id != Guid.Empty))
        {
            if (!seen.Add(item.Id))
            {
                contested.Add(item.Id);
            }
        }

        var incomingIds = seen;
        foreach (var item in heldElsewhere.SelectMany(taskList => taskList.Items))
        {
            if (incomingIds.Contains(item.Id))
            {
                contested.Add(item.Id);
            }
        }

        return contested;
    }
}
