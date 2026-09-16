using Orbit.Contracts.Tasks;
using Orbit.Core.Tasks;

namespace Orbit.Web.Services;

/// <summary>
/// Whether linking one task list to another would close a loop back to where it started.
///
/// The same rule `TaskListLinkValidator` enforces server-side, asked here so the editor never offers a
/// link the save would refuse. Without it the only sign of a loop was a failed save naming a rule
/// nothing on screen had mentioned - and the longer the chain, the less obvious what had gone wrong:
/// A links to B, B to C, and offering C a link back to A looks like any other row in the list.
///
/// The walk itself is Orbit.Core.Tasks.TaskListLinks, shared with the server and the phone. It used to be
/// written out here following links alone, so a loop closed through a way of doing an entry was offered
/// and then refused.
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
        var itemsById = allTaskLists.ToDictionary(taskList => taskList.Id, taskList => taskList.Items);
        return TaskListLinks.WouldCloseALoop(
            listId => itemsById.TryGetValue(listId, out var items)
                ? items.SelectMany(item => item.TaskListIdsItPointsAt)
                : null,
            editedTaskListId, candidateId);
    }
}
