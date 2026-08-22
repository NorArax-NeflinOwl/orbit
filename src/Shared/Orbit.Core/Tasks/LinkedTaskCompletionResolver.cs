namespace Orbit.Core.Tasks;

/// <summary>
/// Resolves every task item that links to another task list to that list's current, live completion,
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
            .Select(item => item.LinkedTaskListId is { } linkedListId
                ? TaskItem.FromPersistence(
                    item.Id, item.Description, item.DueDateUtc, Resolve(linkedListId, context)?.IsCompleted ?? false, item.LinkedTaskListId,
                    item.OverdueNotificationChannel, item.RemindDaily, item.DailyReminderNotificationChannel, item.DailyReminderTimeOfDay)
                : item)
            .ToList();

        var resolvedTaskList = TaskList.FromPersistence(
            taskList.Id, taskList.UserId, taskList.Title, resolvedItems, taskList.CreatedAtUtc, taskList.UpdatedAtUtc,
            taskList.IsShared, taskList.SharedByUserName, taskList.AccessLevel);
        context.Resolved[taskListId] = resolvedTaskList;
        context.Visiting.Remove(taskListId);
        return resolvedTaskList;
    }

    /// <summary>Working state threaded through the recursive resolution of one user's task lists.</summary>
    private sealed class ResolutionContext(IReadOnlyDictionary<Guid, TaskList> taskListsById)
    {
        public IReadOnlyDictionary<Guid, TaskList> TaskListsById { get; } = taskListsById;
        public Dictionary<Guid, TaskList> Resolved { get; } = [];
        public HashSet<Guid> Visiting { get; } = [];
    }
}
