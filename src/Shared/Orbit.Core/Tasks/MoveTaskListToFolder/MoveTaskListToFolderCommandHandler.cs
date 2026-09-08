using Orbit.Core.Abstractions;
using Orbit.Core.Folders;

namespace Orbit.Core.Tasks.MoveTaskListToFolder;

/// <summary>Mirrors MoveNoteToFolderCommandHandler - see it for both checks and why each is here.</summary>
public sealed class MoveTaskListToFolderCommandHandler : IRequestHandler<MoveTaskListToFolderCommand, bool>
{
    private readonly ITaskRepository _taskRepository;
    private readonly IFolderRepository _folderRepository;

    public MoveTaskListToFolderCommandHandler(ITaskRepository taskRepository, IFolderRepository folderRepository)
    {
        _taskRepository = taskRepository;
        _folderRepository = folderRepository;
    }

    public async Task<bool> HandleAsync(MoveTaskListToFolderCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskRepository.GetByIdAsync(request.UserId, request.TaskListId, cancellationToken);
        if (taskList is null || taskList.UserId != request.UserId)
        {
            return false;
        }

        if (request.FolderId is { } folderId
            && await _folderRepository.GetByIdAsync(request.UserId, folderId, cancellationToken) is null)
        {
            return false;
        }

        taskList.MoveToFolder(request.FolderId);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);
        return true;
    }
}
