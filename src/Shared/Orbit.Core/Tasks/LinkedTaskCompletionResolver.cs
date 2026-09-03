namespace Orbit.Core.Tasks;

/// <summary>
/// Resolves every task item that links to other task lists to those lists' current, live completion,
/// since a linked item's own stored completion is never the source of truth for it - see
/// <see cref="TaskItem.Create"/>, which always stores "not completed" for a linked item regardless of
/// what was requested. Resolution is transitive (a linked list can itself contain linked items) and
/// cycle-safe as a defense-in-depth backstop, even though <see cref="TaskListLinkValidator"/> is what
/// actually stops a cycle from being saved in the first place.
/// </summary>
public sealed class LinkedTaskCompletionResolver
{
    /// <summary>
    /// Resolves every task list a user owns in one pass, so a link to any of them - not just the ones
    /// the caller happens to be asking about right now - can be followed to its actual completion.
    /// </summary>
    public IReadOnlyList<TaskList> ResolveAll(IReadOnlyList<TaskList> allUserTaskLists)
    {
        var context = new ResolutionContext(allUserTaskLists.ToDictionary(taskList => taskList.Id));
        foreach (var taskList in allUserTaskLists)
        {
            Resolve(taskList.Id, context);
        }

        return allUserTaskLists.Select(taskList => context.Resolved.GetValueOrDefault(taskList.Id, taskList)).ToList();
    }

    private static TaskList? Resolve(Guid taskListId, ResolutionContext context)
    {
        if (context.Resolved.TryGetValue(taskListId, out var alreadyResolved))
        {
            return alreadyResolved;
        }

        if (!context.TaskListsById.TryGetValue(taskListId, out var taskList) || !context.Visiting.Add(taskListId))
        {
            // Missing (a dangling or foreign link TaskListLinkValidator should have rejected) or
            // mid-resolution already (a cycle it should equally have rejected) - either way there is no
            // safe completion to report, so this link resolves to "not completed".
            return null;
        }

        var resolvedItems = taskList.Items
            .Select(item => item.IsALinkToOtherLists
                ? TaskItem.FromPersistence(
                    item.Id, item.Description, item.DueDateUtc, IsEveryLinkedListDone(item, context), item.LinkedTaskListIds,
                    item.Reminders)
                : item)
            .ToList();

        var resolvedTaskList = TaskList.FromPersistence(
            taskList.Id, taskList.UserId, taskList.Title, resolvedItems, taskList.IsGroup, taskList.IsPrivate, taskList.EncryptedContent,
            taskList.CreatedAtUtc, taskList.UpdatedAtUtc,
            taskList.LockedByUserId, taskList.LockedByUserName, taskList.LockExpiresAtUtc, taskList.Priority, taskList.IsPinned,
            taskList.LinkedWarehouseId, taskList.Description);
        // Every persisted field has to be named above, and every new one has to be added here too - this
        // rebuild is on the path of every read, so a field left out of it is a field that is stored,
        // works in the handler that reads the row directly, and comes back null to the client.
        //
        // IsShared/SharedByUserName/AccessLevel and IsSharedWithOthers are not persisted at all: they
        // are stamped separately per caller (see TaskList's class comment) and would otherwise be lost
        // here - and IsSharedWithOthers is what the phone decides offline editing by, so losing it let
        // a list somebody else can change be edited on a device that cannot hold a lock.
        resolvedTaskList.SetAccessContext(taskList.IsShared, taskList.SharedByUserName, taskList.AccessLevel);
        resolvedTaskList.SetSharedWithOthers(taskList.IsSharedWithOthers);
        context.Resolved[taskListId] = resolvedTaskList;
        context.Visiting.Remove(taskListId);
        return resolvedTaskList;
    }

    /// <summary>
    /// Every list the entry names, or it is not done. An entry standing for several lists is one step -
    /// "the flat is ready" - and a step that reads as finished while one of its lists still has work in
    /// it would be worse than no answer at all. A list that cannot be resolved counts as not done, the
    /// same as a single missing link always did.
    /// </summary>
    private static bool IsEveryLinkedListDone(TaskItem item, ResolutionContext context)
        => item.LinkedTaskListIds.All(linkedListId => Resolve(linkedListId, context)?.IsCompleted ?? false);

    /// <summary>Working state threaded through the recursive resolution of one user's task lists.</summary>
    private sealed class ResolutionContext(IReadOnlyDictionary<Guid, TaskList> taskListsById)
    {
        public IReadOnlyDictionary<Guid, TaskList> TaskListsById { get; } = taskListsById;
        public Dictionary<Guid, TaskList> Resolved { get; } = [];
        public HashSet<Guid> Visiting { get; } = [];
    }
}
