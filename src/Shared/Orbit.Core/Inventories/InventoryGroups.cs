namespace Orbit.Core.Inventories;

/// <summary>
/// Reading a group shelf: which shelves it gathers, however deep that goes, and whether a proposed
/// membership would make a ring.
///
/// A ring is the one thing gathering has to refuse. "The pantry gathers the cellar, the cellar gathers
/// the pantry" is not a mistake a page can draw its way out of - everything that walks a group would
/// walk it forever - and only a save can see both sides of it, since the shelf being edited knows what
/// it gathers and nothing else. The same job TaskListLinkValidator does for lists that gather lists.
/// </summary>
public sealed class InventoryGroups(IInventoryRepository inventoryRepository)
{
    /// <summary>
    /// Whether <paramref name="inventoryId"/> may gather <paramref name="wanted"/>. False when one of
    /// them gathers this shelf back, directly or through another - and when one of them is not this
    /// reader's to gather at all, which is the same answer a task list gives about a list it cannot see.
    /// </summary>
    public async Task<bool> MayGatherAsync(
        Guid ownerId, Guid inventoryId, IReadOnlyList<Guid> wanted, CancellationToken cancellationToken)
    {
        if (wanted.Count == 0)
        {
            return true;
        }

        var theirs = (await inventoryRepository.GetAllAsync(ownerId, updatedSinceUtc: null, cancellationToken))
            .ToDictionary(inventory => inventory.Id);
        if (wanted.Any(gathered => gathered == inventoryId || !theirs.ContainsKey(gathered)))
        {
            return false;
        }

        // Walking from each wanted member: if any of them can reach this shelf, gathering it would close
        // a ring. The visited set is what stops the walk on a ring that somehow already exists rather
        // than hanging on it - a save refuses them, so this is for the day one slips through.
        var walked = new HashSet<Guid>();
        var toWalk = new Stack<Guid>(wanted);
        while (toWalk.Count > 0)
        {
            var next = toWalk.Pop();
            if (next == inventoryId)
            {
                return false;
            }

            if (!walked.Add(next) || !theirs.TryGetValue(next, out var inventory))
            {
                continue;
            }

            foreach (var deeper in inventory.GathersInventoryIds)
            {
                toWalk.Push(deeper);
            }
        }

        return true;
    }

    /// <summary>
    /// Every shelf a group stands for, in the order they are read - its own members first, then what
    /// each of them gathers. The group itself is not among them: it is the thing being read, not one of
    /// the things it holds.
    ///
    /// A shelf reached twice is listed once, by the first way it was reached: two members both gathering
    /// the same cupboard is one cupboard, and drawing it twice would double everything it holds.
    /// </summary>
    public async Task<IReadOnlyList<Inventory>> GatheredByAsync(
        Guid ownerId, Inventory group, CancellationToken cancellationToken)
    {
        if (!group.IsGroup)
        {
            return [];
        }

        var theirs = (await inventoryRepository.GetAllAsync(ownerId, updatedSinceUtc: null, cancellationToken))
            .ToDictionary(inventory => inventory.Id);

        var gathered = new List<Inventory>();
        var seen = new HashSet<Guid> { group.Id };
        Walk(group, theirs, seen, gathered);
        return gathered;
    }

    private static void Walk(
        Inventory inventory, IReadOnlyDictionary<Guid, Inventory> theirs, HashSet<Guid> seen,
        List<Inventory> gathered)
    {
        foreach (var memberId in inventory.GathersInventoryIds)
        {
            // A member the reader no longer has - deleted, or a share withdrawn - is passed over rather
            // than being a failure, the same way a link to a deleted task list reads as "nothing there".
            if (!seen.Add(memberId) || !theirs.TryGetValue(memberId, out var member))
            {
                continue;
            }

            gathered.Add(member);
            Walk(member, theirs, seen, gathered);
        }
    }
}
