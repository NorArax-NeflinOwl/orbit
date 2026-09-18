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
/// It is offered when the list has **work** on it - anything that is not merely a row pointing at
/// another list - directly or through such a row, however deep that goes. Only a list made of links to
/// lists with nothing on them is refused: there is genuinely nothing for a shelf to hold.
///
/// **It used to ask for a product** (2026-09-09): an entry of the Inventory kind, or one already
/// pointing at a shelf item. That read "a list of plain errands has nothing a shelf would be about",
/// and it is wrong about the commonest case there is - a shopping list of plain lines is exactly what
/// somebody wants a shelf built from, and the endpoint builds one: it counts every entry the tree
/// names, product or not (StockRequirementCounter). So the rule hid the offer from the lists it was
/// most useful on, which is what the user reported on 2026-09-18 as not being able to generate an
/// inventory from a list at all.
///
/// The question is asked of the clients rather than of the server: the endpoint builds a shelf from
/// whatever the work names (see GenerateInventoryFromTaskListCommandHandler), and this is about when a
/// reader is invited to press it.
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
        // Work is anything that is not only a link: a line somebody has to do something about, whether
        // or not it says which shelf item it means - see the summary above.
        if (entries.Any(entry => entry.LinkedTaskListIds.Count == 0))
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
