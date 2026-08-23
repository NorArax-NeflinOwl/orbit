using Orbit.Core.Users;

namespace Orbit.Core.Tasks;

/// <summary>Mirrors Orbit.Core.Notes.NoteAccessResolver - see its class comment.</summary>
public sealed class TaskListAccessResolver
{
    private readonly ITaskRepository _taskRepository;
    private readonly ITaskListShareRepository _taskListShareRepository;
    private readonly IUserRepository _userRepository;

    public TaskListAccessResolver(ITaskRepository taskRepository, ITaskListShareRepository taskListShareRepository, IUserRepository userRepository)
    {
        _taskRepository = taskRepository;
        _taskListShareRepository = taskListShareRepository;
        _userRepository = userRepository;
    }

    /// <summary>Null when callerId neither owns taskListId nor has an accepted share of it.</summary>
    public async Task<TaskList?> ResolveAsync(Guid callerId, Guid taskListId, CancellationToken cancellationToken)
    {
        var ownedTaskList = await _taskRepository.GetByIdAsync(callerId, taskListId, cancellationToken);
        if (ownedTaskList is not null)
        {
            return ownedTaskList;
        }

        var grant = await _taskListShareRepository.FindAcceptedGrantAsync(taskListId, callerId, cancellationToken);
        if (grant is null)
        {
            return null;
        }

        var taskList = await _taskRepository.GetByIdAsync(grant.OwnerUserId, grant.SourceTaskListId, cancellationToken);
        if (taskList is null)
        {
            return null;
        }

        var owner = await _userRepository.GetByIdAsync(grant.OwnerUserId, cancellationToken);
        taskList.SetAccessContext(isShared: true, owner?.UserName, grant.AccessLevel);
        return taskList;
    }

    /// <summary>Every task list callerId owns, plus every task list shared with them (accepted grants only).</summary>
    public async Task<IReadOnlyList<TaskList>> ResolveAllAsync(Guid callerId, CancellationToken cancellationToken)
    {
        var owned = await _taskRepository.GetAllAsync(callerId, cancellationToken);
        var grants = await _taskListShareRepository.GetAcceptedGrantsForRecipientAsync(callerId, cancellationToken);

        var granted = new List<TaskList>();
        foreach (var grant in grants)
        {
            var taskList = await _taskRepository.GetByIdAsync(grant.OwnerUserId, grant.SourceTaskListId, cancellationToken);
            if (taskList is null)
            {
                continue;
            }

            var owner = await _userRepository.GetByIdAsync(grant.OwnerUserId, cancellationToken);
            taskList.SetAccessContext(isShared: true, owner?.UserName, grant.AccessLevel);
            granted.Add(taskList);
        }

        return owned.Concat(granted).ToList();
    }
}
