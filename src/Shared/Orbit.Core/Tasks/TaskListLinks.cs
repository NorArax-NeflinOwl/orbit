namespace Orbit.Core.Tasks;

/// <summary>
/// Whether pointing a list's entry at another list would close a loop back to where it started - the
/// rule <see cref="TaskListLinkValidator"/> refuses a save on, kept in one place so every client can ask
/// it before offering a list rather than learning it from a refusal.
///
/// Over ids and a lookup rather than over task lists, because each side holds lists in its own shape:
/// the server has <see cref="TaskList"/>, a browser the DTOs it was sent, a phone its local rows. Asked
/// about what a list points at in every way it can - a list it stands for and a way of doing it that is a
/// list (see <see cref="TaskItem.TaskListIdsItPointsAt"/>) - since either closes a loop as surely. A
/// browser that followed links alone once offered a loop through a way, and the phone offered one outright.
///
/// A loop would make completion resolution walk forever (see <see cref="LinkedTaskCompletionResolver"/>),
/// which is why this is a rule and not a matter of taste.
/// </summary>
public static class TaskListLinks
{
    /// <summary>
    /// Whether an entry on <paramref name="editedTaskListId"/> pointing at <paramref name="candidateId"/>
    /// would close a loop - whether following what the candidate points at, however far, ever arrives
    /// back at the list being edited. Pointing a list at itself is the shortest loop there is.
    /// </summary>
    /// <param name="listsItPointsAt">
    /// Every list the given list's entries point at, or null for a list this side cannot see - which
    /// ends that branch rather than the walk, since an unreadable list cannot be followed and the server
    /// refuses a link to it for its own reasons.
    /// </param>
    public static bool WouldCloseALoop(
        Func<Guid, IEnumerable<Guid>?> listsItPointsAt, Guid editedTaskListId, Guid candidateId)
    {
        var visited = new HashSet<Guid>();
        var toVisit = new Queue<Guid>([candidateId]);

        while (toVisit.Count > 0)
        {
            var currentId = toVisit.Dequeue();
            if (currentId == editedTaskListId)
            {
                return true;
            }

            if (!visited.Add(currentId) || listsItPointsAt(currentId) is not { } pointedAt)
            {
                continue;
            }

            foreach (var linkedId in pointedAt)
            {
                toVisit.Enqueue(linkedId);
            }
        }

        return false;
    }
}
