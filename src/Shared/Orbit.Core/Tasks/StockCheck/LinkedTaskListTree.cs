namespace Orbit.Core.Tasks.StockCheck;

/// <summary>
/// Gathers a list and everything linked below it - the lists its items point at, the lists those point
/// at, and so on. The same walk the checklist screen draws, done here because the stock check has to
/// count the whole tree's work, not just the top list's.
///
/// A list is walked because of what is on it rather than because of how it is being read: an entry that
/// stands for another list is a link whether or not the group view is on - see <see cref="Append"/>.
/// </summary>
public static class LinkedTaskListTree
{
    /// <summary>
    /// <paramref name="root"/> and every list reachable from it through linked entries, each appearing
    /// once. A list that links back to one of its ancestors - or to itself - stops at the repeat rather
    /// than unfolding forever.
    /// </summary>
    public static IReadOnlyList<TaskList> Flatten(TaskList root, IReadOnlyCollection<TaskList> candidates)
    {
        var byId = candidates.GroupBy(list => list.Id).ToDictionary(group => group.Key, group => group.First());
        var gathered = new List<TaskList>();
        Append(root, byId, [], gathered);
        return gathered;
    }

    /// <summary>Every piece of work in the tree - the entries that are things to do rather than links.</summary>
    public static IReadOnlyList<TaskItem> WorkIn(TaskList root, IReadOnlyCollection<TaskList> candidates)
        => [.. Flatten(root, candidates).SelectMany(list => list.Items).Where(item => !item.IsALinkToOtherLists)];

    private static void Append(
        TaskList taskList, IReadOnlyDictionary<Guid, TaskList> byId, HashSet<Guid> alreadyGathered, List<TaskList> gathered)
    {
        if (!alreadyGathered.Add(taskList.Id))
        {
            return;
        }

        gathered.Add(taskList);

        // Whatever the group view says. It used to stop here on a list whose IsGroup was off, which was
        // the same thing until 2026-09-16: the box ticked itself the moment an entry came to stand for
        // another list and could not be unticked. It is the reader's now (see TaskList.IsGroup), and a
        // list they had turned it off on stopped being walked at all - so the stock check counted the
        // top list alone and an inventory generated from it held nothing any sublist asked for.
        // Reported on 2026-09-18. What a list is made of is its entries, not how somebody is reading it.
        foreach (var linkedId in taskList.Items.SelectMany(item => item.LinkedTaskListIds))
        {
            if (byId.TryGetValue(linkedId, out var linked))
            {
                Append(linked, byId, alreadyGathered, gathered);
            }
        }
    }
}
