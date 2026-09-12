namespace Orbit.Core.Tasks;

/// <summary>
/// Keeps reference groups in step - the entries that are one object on several lists, picked from the
/// name suggestions (see <see cref="TaskItem.ReferencesTaskItemId"/>).
///
/// Every member keeps the shared details itself rather than reading them off the source, so a list is
/// read, synced and drawn exactly as it always was. What this does instead is pass them on when any member
/// is saved. It also keeps the pointers straight: a reference always names the group's source, never a
/// member that points further on, and one naming nothing that exists is an entry of its own again. When the
/// source goes, the member created first takes its place, so the group does not fall apart with it.
///
/// Private lists take no part: the server holds none of their entries, so it can neither read nor write a
/// member there.
/// </summary>
public sealed class TaskItemReferences(ITaskRepository taskRepository)
{
    /// <summary>
    /// Gives every incoming entry its creation time: the one it already had, found by id among what is
    /// stored, or now for an entry this save is the first to store.
    /// </summary>
    public static void StampCreationTimes(
        IReadOnlyList<TaskItem> incoming, IReadOnlyList<TaskItem> stored, DateTimeOffset nowUtc)
    {
        var storedById = stored.GroupBy(item => item.Id).ToDictionary(group => group.Key, group => group.First());
        foreach (var item in incoming)
        {
            item.StampCreation(storedById.TryGetValue(item.Id, out var storedItem) ? storedItem.CreatedAtUtc : nowUtc);
        }
    }

    /// <summary>
    /// Settles every reference group a save touches, and answers the owner's other lists it changed, for
    /// the caller to write in the same save as its own.
    /// </summary>
    /// <param name="saved">The list being saved, not yet written; null when lists are only being deleted.</param>
    /// <param name="referencesBefore">
    /// What each of the saved list's entries was the same thing as before this save, by id - empty for a
    /// new list. An entry that points somewhere it did not before has just joined its group, and takes on
    /// what the group says rather than passing on what it says: see <see cref="TakeOnWhatTheirGroupSays"/>.
    /// </param>
    /// <param name="removedItemIds">Entries this save takes away. One still found on another list was moved, not removed.</param>
    /// <param name="goneListIds">Lists about to be deleted, every entry of which goes with them.</param>
    public async Task<IReadOnlyList<TaskList>> SettleAsync(
        Guid ownerId, TaskList? saved, IReadOnlyDictionary<Guid, Guid?> referencesBefore,
        IReadOnlySet<Guid> removedItemIds, IReadOnlySet<Guid> goneListIds, CancellationToken cancellationToken)
    {
        if (saved is { IsPrivate: true })
        {
            return [];
        }

        var everyList = await taskRepository.GetAllAsync(ownerId, updatedSinceUtc: null, cancellationToken);
        var removed = removedItemIds
            .Concat(everyList.Where(taskList => goneListIds.Contains(taskList.Id)).SelectMany(taskList => taskList.Items).Select(item => item.Id))
            .ToHashSet();
        List<TaskList> lists =
        [
            .. saved is null ? [] : new[] { saved },
            .. everyList.Where(taskList => taskList.Id != saved?.Id && !goneListIds.Contains(taskList.Id) && !taskList.IsPrivate)
        ];
        var members = lists.SelectMany(taskList => taskList.Items.Select(item => new Member(taskList, item))).ToList();
        var changed = new HashSet<TaskList>(ReferenceEqualityComparer.Instance);

        var joined = new HashSet<Guid>();
        if (saved is not null)
        {
            PointAtTheirSource(saved, members);
            joined = saved.Items
                .Where(item => item.ReferencesTaskItemId is { } now
                    && (!referencesBefore.TryGetValue(item.Id, out var before) || before != now))
                .Select(item => item.Id)
                .ToHashSet();
            TakeOnWhatTheirGroupSays(saved, joined, members);
        }

        HandOverWhatWasRemoved(removed, members, changed);
        if (saved is not null)
        {
            PassOnWhatWasSaved(saved, joined, members, changed);
        }

        changed.RemoveWhere(taskList => ReferenceEquals(taskList, saved));
        foreach (var taskList in changed)
        {
            // Rebuilt with what it already says, so it counts as changed and a phone holding it pulls it again.
            taskList.Update(
                taskList.Title, taskList.Items, taskList.IsGroup, taskList.IsPrivate, taskList.EncryptedContent,
                taskList.Priority, taskList.Description);
        }

        return [.. changed];
    }

    private sealed record Member(TaskList TaskList, TaskItem Item);

    /// <summary>
    /// Each entry of the saved list that points somewhere is pointed at its group's source: a member that
    /// points further on is followed, and one that points at nothing there is - or round in a loop - makes
    /// the entry one of its own again.
    /// </summary>
    private static void PointAtTheirSource(TaskList saved, IReadOnlyList<Member> members)
    {
        var byId = members.GroupBy(member => member.Item.Id).ToDictionary(group => group.Key, group => group.First().Item);
        foreach (var item in saved.Items.Where(item => item.ReferencesTaskItemId is not null))
        {
            item.PointReferenceAt(SourceOf(item.ReferencesTaskItemId!.Value, byId));
        }
    }

    private static Guid? SourceOf(Guid pointedAt, IReadOnlyDictionary<Guid, TaskItem> byId)
    {
        var visited = new HashSet<Guid>();
        var current = pointedAt;
        while (visited.Add(current))
        {
            if (!byId.TryGetValue(current, out var entry))
            {
                return null;
            }

            if (entry.ReferencesTaskItemId is not { } further)
            {
                return current;
            }

            current = further;
        }

        return null;
    }

    /// <summary>
    /// A source that is gone hands its role to the member created first - the user's rule - and the rest
    /// of the group is pointed at that one. Nothing is lost: every member already carries the details.
    /// </summary>
    private static void HandOverWhatWasRemoved(IReadOnlySet<Guid> removed, IReadOnlyList<Member> members, ISet<TaskList> changed)
    {
        foreach (var goneId in removed.Where(id => members.All(member => member.Item.Id != id)))
        {
            var group = members.Where(member => member.Item.ReferencesTaskItemId == goneId).ToList();
            if (group.Count == 0)
            {
                continue;
            }

            var heir = group
                .OrderBy(member => member.Item.CreatedAtUtc ?? DateTimeOffset.MaxValue)
                .ThenBy(member => member.Item.Id)
                .First();
            foreach (var member in group)
            {
                member.Item.PointReferenceAt(ReferenceEquals(member.Item, heir.Item) ? null : heir.Item.Id);
                changed.Add(member.TaskList);
            }
        }
    }

    /// <summary>
    /// An entry that has just joined a group takes on what the group says, rather than the other way round.
    /// Whoever picked the name may know only its words - a phone does, until the list comes back - and a save
    /// of those alone must not empty the details every other member carries.
    /// </summary>
    private static void TakeOnWhatTheirGroupSays(TaskList saved, IReadOnlySet<Guid> joined, IReadOnlyList<Member> members)
    {
        foreach (var item in saved.Items.Where(item => joined.Contains(item.Id)))
        {
            if (members.FirstOrDefault(member => member.Item.Id == item.ReferencesTaskItemId) is { } source)
            {
                item.TakeSharedDetailsFrom(source.Item);
            }
        }
    }

    /// <summary>
    /// What every entry of the saved list says about its group's shared details, passed on to the rest of
    /// that group - except an entry that has only just joined it, which took the group's instead.
    /// </summary>
    private static void PassOnWhatWasSaved(
        TaskList saved, IReadOnlySet<Guid> joined, IReadOnlyList<Member> members, ISet<TaskList> changed)
    {
        foreach (var item in saved.Items.Where(item => !joined.Contains(item.Id)))
        {
            var source = item.ReferencesTaskItemId ?? item.Id;
            foreach (var member in members.Where(member =>
                         !ReferenceEquals(member.Item, item) && (member.Item.ReferencesTaskItemId ?? member.Item.Id) == source))
            {
                if (member.Item.TakeSharedDetailsFrom(item))
                {
                    changed.Add(member.TaskList);
                }
            }
        }
    }
}
