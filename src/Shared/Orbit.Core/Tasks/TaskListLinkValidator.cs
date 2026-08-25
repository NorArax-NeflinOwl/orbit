using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks;

/// <summary>
/// Validates the links task items make to other task lists before a create or update is allowed to
/// save: a linked list must exist and be owned by the same user, an item can't link to the list it
/// belongs to, and a link can't create a cycle - which would make
/// <see cref="LinkedTaskCompletionResolver"/>'s completion resolution loop forever without this check.
/// </summary>
public sealed class TaskListLinkValidator
{
    private readonly ITaskRepository _taskRepository;

    public TaskListLinkValidator(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    /// <summary>
    /// <paramref name="taskListId"/> is the id of the task list being saved - null for a new one, since
    /// nothing can yet link back to a list that doesn't have an id.
    /// </summary>
    public async Task ValidateAsync(Guid userId, Guid? taskListId, IReadOnlyList<TaskItem> items, CancellationToken cancellationToken)
    {
        var linkedListIds = items.Select(item => item.LinkedTaskListId).OfType<Guid>().Distinct().ToList();
        if (linkedListIds.Count == 0)
        {
            return;
        }

        var taskListsById = (await _taskRepository.GetAllAsync(userId, cancellationToken))
            .ToDictionary(taskList => taskList.Id);

        foreach (var linkedListId in linkedListIds)
        {
            if (linkedListId == taskListId)
            {
                throw new InvalidRequestException("A task list item can't link to the list it belongs to.");
            }

            if (!taskListsById.ContainsKey(linkedListId))
            {
                throw new InvalidRequestException("A linked task list must exist and belong to the same user.");
            }

            if (taskListId is { } currentListId && Reaches(linkedListId, currentListId, taskListsById))
            {
                throw new InvalidRequestException("This link would create a cycle between task lists.");
            }
        }
    }

    /// <summary>
    /// True if, starting from <paramref name="fromId"/> and following linked items transitively, the
    /// walk ever reaches <paramref name="toId"/> - i.e. whether linking toId's item to fromId would
    /// close a loop back to where it started.
    /// </summary>
    private static bool Reaches(Guid fromId, Guid toId, IReadOnlyDictionary<Guid, TaskList> taskListsById)
    {
        var visited = new HashSet<Guid>();
        var toVisit = new Queue<Guid>();
        toVisit.Enqueue(fromId);

        while (toVisit.Count > 0)
        {
            var currentId = toVisit.Dequeue();
            if (currentId == toId)
            {
                return true;
            }

            if (!visited.Add(currentId) || !taskListsById.TryGetValue(currentId, out var currentList))
            {
                continue;
            }

            foreach (var linkedId in currentList.Items.Select(item => item.LinkedTaskListId).OfType<Guid>())
            {
                toVisit.Enqueue(linkedId);
            }
        }

        return false;
    }
}
