using Orbit.Core.Abstractions;
using Orbit.Core.Users;

namespace Orbit.Core.Tasks.AcquireTaskListLock;

/// <summary>Mirrors Orbit.Core.Notes.AcquireNoteLock.AcquireNoteLockCommandHandler - see its comment.</summary>
public sealed class AcquireTaskListLockCommandHandler : IRequestHandler<AcquireTaskListLockCommand, EditOutcome>
{
    private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(60);

    private readonly TaskListAccessResolver _taskListAccessResolver;
    private readonly ITaskRepository _taskRepository;
    private readonly IUserRepository _userRepository;

    public AcquireTaskListLockCommandHandler(TaskListAccessResolver taskListAccessResolver, ITaskRepository taskRepository, IUserRepository userRepository)
    {
        _taskListAccessResolver = taskListAccessResolver;
        _taskRepository = taskRepository;
        _userRepository = userRepository;
    }

    public async Task<EditOutcome> HandleAsync(AcquireTaskListLockCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskListAccessResolver.ResolveAsync(request.UserId, request.TaskListId, cancellationToken);
        if (taskList is null || !taskList.AccessLevel.AllowsEditing())
        {
            return EditOutcome.NotFound;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        if (taskList.IsLockedByAnotherUser(request.UserId, nowUtc))
        {
            return EditOutcome.LockedBy(taskList.LockedByUserName!);
        }

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        taskList.AcquireLock(request.UserId, user!.UserName, nowUtc, LockDuration);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);
        return EditOutcome.Success;
    }
}
