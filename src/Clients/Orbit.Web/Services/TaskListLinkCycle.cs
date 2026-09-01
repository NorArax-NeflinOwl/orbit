using Orbit.Contracts.Tasks;

namespace Orbit.Web.Services;

/// <summary>
/// Whether linking one task list to another would close a loop back to where it started.
///
/// The same rule `TaskListLinkValidator` enforces server-side, asked here so the editor never offers a
/// link the save would refuse. Without it the only sign of a loop was a failed save naming a rule
/// nothing on screen had mentioned - and the longer the chain, the less obvious what had gone wrong:
/// A links to B, B to C, and offering C a link back to A looks like any other row in the list.
///
/// A loop would make completion resolution walk forever (see LinkedTaskCompletionResolver), which is
/// why the rule exists at all rather than being a matter of taste.
/// </summary>
public static class TaskListLinkCycle
{
    /// <summary>
    /// Whether an item on <paramref name="editedTaskListId"/> linking to <paramref name="candidateId"/>
    /// would close a loop - that is, whether following links out of the candidate ever arrives back at
    /// the list being edited. Judged against the lists as they are saved, which is what the server will
    /// judge against too.
    /// </summary>
    public static bool WouldClose(
        IReadOnlyList<TaskDto> allTaskLists, Guid editedTaskListId, Guid candidateId)
    {
        if (candidateId == editedTaskListId)
        {
            return true;
        }

        var itemsById = allTaskLists.ToDictionary(taskList => taskList.Id, taskList => taskList.Items);
        var visited = new HashSet<Guid>();
        var toVisit = new Queue<Guid>([candidateId]);

        while (toVisit.Count > 0)
        {
            var currentId = toVisit.Dequeue();
            if (currentId == editedTaskListId)
            {
                return true;
            }

            // A list already walked, or one this reader cannot see, ends that branch rather than the
            // walk: an unreadable list cannot be followed, and the server will refuse a link to it
            // for its own reasons.
            if (!visited.Add(currentId) || !itemsById.TryGetValue(currentId, out var items))
            {
                continue;
            }

            foreach (var linkedId in items.SelectMany(item => item.AllLinkedTaskListIds))
            {
                toVisit.Enqueue(linkedId);
            }
        }

        return false;
    }
}
