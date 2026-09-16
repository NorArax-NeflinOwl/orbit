using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.ArchiveTaskList;

/// <summary>
/// Only the owner puts a list away, and only their own. A recipient must not: one row is one list, so
/// archiving one shared with them would take it off its owner's page - the same reason they cannot file
/// or pin one. Mirrors MoveTaskListToFolderCommandHandler on who may.
/// </summary>
public sealed class ArchiveTaskListCommandHandler : IRequestHandler<ArchiveTaskListCommand, bool>
{
    private readonly ITaskRepository _taskLists;

    public ArchiveTaskListCommandHandler(ITaskRepository taskLists) => _taskLists = taskLists;

    public async Task<bool> HandleAsync(ArchiveTaskListCommand request, CancellationToken cancellationToken)
    {
        var found = await _taskLists.GetByIdAsync(request.UserId, request.TaskListId, cancellationToken);
        if (found is null || found.UserId != request.UserId)
        {
            return false;
        }

        found.Archive(request.IsArchived);
        await _taskLists.UpdateAsync(found, cancellationToken);
        return true;
    }
}
