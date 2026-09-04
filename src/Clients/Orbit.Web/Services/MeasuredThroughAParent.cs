using Orbit.Contracts.Tasks;

namespace Orbit.Web.Services;

/// <summary>
/// Which task lists a storage is measured against without anybody having said so about them directly.
///
/// A list measured against an inventory is asking that shelf for what its entries need. A **group** list
/// asks for what every list it gathers needs - that is what gathering them means - so measuring the
/// group measures the whole tree under it, however deep. The editor's own checklist only ever knew about
/// the direct tie (TaskDto.LinkedInventoryId), so a shelf serving a group of a dozen lists showed one
/// box ticked and eleven unticked, which reads as "these eleven are not asking for anything here" - the
/// opposite of what is true.
///
/// This answers the display half of that: which lists are reached, and which gathered list each one was
/// reached through, so a row can say so. It deliberately does **not** write anything: the tie still
/// belongs to the list at the top of the tree, and ticking a group's children individually would make
/// eleven ties that have to be kept in step with a membership that changes underneath them.
/// </summary>
public static class MeasuredThroughAParent
{
    /// <summary>
    /// Every list reached by following links out of <paramref name="measuredDirectly"/>, mapped to the
    /// list it was reached through - the one somebody actually ticked, not the nearest parent, since
    /// that is the one the row has to name.
    ///
    /// A list that is itself measured directly is left out: it stands on its own tie and the checklist
    /// already draws it that way. Cycles end a branch rather than the walk, the way
    /// <see cref="TaskListLinkCycle"/> handles them - the server refuses a loop, but a client should not
    /// hang on data that somehow holds one.
    /// </summary>
    public static IReadOnlyDictionary<Guid, Guid> Reach(
        IReadOnlyList<TaskDto> allTaskLists, IReadOnlySet<Guid> measuredDirectly)
    {
        var itemsById = allTaskLists.ToDictionary(taskList => taskList.Id, taskList => taskList.Items);
        var reachedThrough = new Dictionary<Guid, Guid>();

        foreach (var rootId in measuredDirectly)
        {
            var visited = new HashSet<Guid> { rootId };
            var toVisit = new Queue<Guid>([rootId]);

            while (toVisit.Count > 0)
            {
                if (!itemsById.TryGetValue(toVisit.Dequeue(), out var items))
                {
                    // A list this reader cannot see ends that branch: there is nothing to follow, and
                    // nothing to draw a row for either.
                    continue;
                }

                foreach (var linkedId in items.SelectMany(item => item.AllLinkedTaskListIds))
                {
                    if (!visited.Add(linkedId))
                    {
                        continue;
                    }

                    toVisit.Enqueue(linkedId);

                    // Directly measured lists keep their own tie, and the first root to reach a list is
                    // the one named: two groups gathering the same list is a fact about the lists rather
                    // than about this shelf, and naming both would say more than the row has room for.
                    if (!measuredDirectly.Contains(linkedId))
                    {
                        reachedThrough.TryAdd(linkedId, rootId);
                    }
                }
            }
        }

        return reachedThrough;
    }
}
