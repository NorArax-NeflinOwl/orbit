using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.ShareTaskList;

/// <summary>Mirrors Orbit.Core.Notes.ShareNote.ShareNoteCommandHandler - see its class comment for the permission rules enforced here.</summary>
public sealed class ShareTaskListCommandHandler : IRequestHandler<ShareTaskListCommand, ShareOutcome?>
{
    private readonly ITaskRepository _taskRepository;
    private readonly ITaskListShareRepository _taskListShareRepository;

    public ShareTaskListCommandHandler(ITaskRepository taskRepository, ITaskListShareRepository taskListShareRepository)
    {
        _taskRepository = taskRepository;
        _taskListShareRepository = taskListShareRepository;
    }

    public async Task<ShareOutcome?> HandleAsync(ShareTaskListCommand request, CancellationToken cancellationToken)
    {
        var sourceTaskList = await _taskRepository.GetByIdAsync(request.OwnerUserId, request.TaskListId, cancellationToken);
        if (sourceTaskList is null)
        {
            return null;
        }

        var originalOwnerUserId = sourceTaskList.EffectiveOwnerUserId;
        if (request.RecipientUserId == originalOwnerUserId)
        {
            return null;
        }

        if (sourceTaskList.IsShared
            && (sourceTaskList.AccessLevel < ShareAccessLevel.Share || request.AccessLevel > sourceTaskList.AccessLevel))
        {
            return null;
        }

        var existingShare = await _taskListShareRepository.FindExistingAsync(sourceTaskList.Id, request.RecipientUserId, cancellationToken);
        if (existingShare is not null)
        {
            return new ShareOutcome(existingShare.Id, AlreadyShared: true);
        }

        var share = TaskListShare.Create(sourceTaskList.Id, request.OwnerUserId, request.RecipientUserId, originalOwnerUserId, request.AccessLevel);
        await _taskListShareRepository.AddAsync(share, cancellationToken);
        return new ShareOutcome(share.Id, AlreadyShared: false);
    }
}
