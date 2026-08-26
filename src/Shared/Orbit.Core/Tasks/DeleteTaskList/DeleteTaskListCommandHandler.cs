using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.DeleteTaskList;

public sealed class DeleteTaskListCommandHandler : IRequestHandler<DeleteTaskListCommand, bool>
{
    private readonly ITaskRepository _taskRepository;
    private readonly ITaskListShareRepository _taskListShareRepository;

    public DeleteTaskListCommandHandler(
        ITaskRepository taskRepository, ITaskListShareRepository taskListShareRepository)
    {
        _taskRepository = taskRepository;
        _taskListShareRepository = taskListShareRepository;
    }

    /// <summary>
    /// Deletes the caller's own task list, or - when it is somebody else's, shared with them - takes it
    /// off their list by dropping the grant. False when it is neither, so the API answers 404 without
    /// leaking which of the two it was. Other
    /// task lists that link one of their items to this one are left with a dangling
    /// <see cref="TaskItem.LinkedTaskListId"/> - <see cref="LinkedTaskCompletionResolver"/> already
    /// treats a link to a missing list as "not completed" rather than failing, so this is safe.
    /// </summary>
    public async Task<bool> HandleAsync(DeleteTaskListCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken);
        if (taskList is null)
        {
            // Not the owner's. A recipient asking to be rid of something shared with them means
            // taking it off their own list - destroying somebody else's task list is not theirs to
            // do. Removing the accepted grant does exactly that and leaves the owner's untouched.
            if (await _taskListShareRepository.FindAcceptedGrantAsync(request.Id, request.UserId, cancellationToken) is not null)
            {
                await _taskListShareRepository.RemoveAcceptedGrantAsync(request.Id, request.UserId, cancellationToken);
                return true;
            }

            return false;
        }

        await _taskRepository.DeleteAsync(request.UserId, request.Id, cancellationToken);
        return true;
    }
}
