namespace Orbit.Core.Tasks.StockCheck;

/// <summary>
/// Gathers a group list and everything linked below it - the lists its items point at, the lists those
/// point at, and so on. The same walk the checklist screen draws, done here because the stock check has
/// to count the whole tree's work, not just the top list's.
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
        if (!taskList.IsGroup)
        {
            return;
        }

        foreach (var linkedId in taskList.Items.SelectMany(item => item.LinkedTaskListIds))
        {
            if (byId.TryGetValue(linkedId, out var linked))
            {
                Append(linked, byId, alreadyGathered, gathered);
            }
        }
    }
}
