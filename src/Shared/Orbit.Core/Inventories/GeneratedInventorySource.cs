namespace Orbit.Core.Inventories;

/// <summary>
/// One entry, reduced to the two things this question needs: whether it describes something to put on
/// a shelf, and which lists it stands for. Each client maps its own shape onto this rather than the
/// rule being written twice.
/// </summary>
/// <param name="IsAProduct">
/// Whether the entry is an Inventory one - see Orbit.Core.Tasks.TaskItemKind. An entry that already
/// points at a shelf item counts too: it is about a product either way.
/// </param>
public readonly record struct TaskEntrySummary(bool IsAProduct, IReadOnlyList<Guid> LinkedTaskListIds);

/// <summary>
/// Whether building a storage out of a task list is worth offering at all.
///
/// It is offered when the list has something on it that describes a product - directly, or through an
/// entry standing for another list that has one, however deep that goes. A list of plain errands has
/// nothing a shelf would be about, and the menu entry on it was an offer to build an empty storage and
/// quietly point the list at it.
///
/// The question is asked of the clients rather than of the server: the endpoint still builds a shelf
/// from whatever the work names (see GenerateInventoryFromTaskListCommandHandler), and this is about
/// when a reader is invited to press it.
/// </summary>
public static class GeneratedInventorySource
{
    /// <param name="entriesOn">
    /// The entries of a list this one stands for, or null for a list this client has not got - one that
    /// was never synced, or whose share was withdrawn. Null is read as "nothing to build from", which is
    /// all that can honestly be said about a list nobody here can see.
    /// </param>
    public static bool HasSomethingToBuildFrom(
        IReadOnlyList<TaskEntrySummary> entries, Func<Guid, IReadOnlyList<TaskEntrySummary>?> entriesOn)
        => HasSomethingToBuildFrom(entries, entriesOn, []);

    private static bool HasSomethingToBuildFrom(
        IReadOnlyList<TaskEntrySummary> entries,
        Func<Guid, IReadOnlyList<TaskEntrySummary>?> entriesOn,
        HashSet<Guid> alreadyWalked)
    {
        if (entries.Any(entry => entry.IsAProduct))
        {
            return true;
        }

        foreach (var linkedTaskListId in entries.SelectMany(entry => entry.LinkedTaskListIds))
        {
            // A pair of lists standing for each other is refused when it is made (see
            // TaskListLinkValidator) and would hang here if one ever slipped through - the same reason
            // deleting a tree carries a visited set.
            if (!alreadyWalked.Add(linkedTaskListId))
            {
                continue;
            }

            if (entriesOn(linkedTaskListId) is { } linked
                && HasSomethingToBuildFrom(linked, entriesOn, alreadyWalked))
            {
                return true;
            }
        }

        return false;
    }
}
