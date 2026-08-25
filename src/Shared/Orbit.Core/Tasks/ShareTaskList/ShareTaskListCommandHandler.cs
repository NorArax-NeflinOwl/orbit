using Orbit.Core.Abstractions;

using Orbit.Core.Notifications;

namespace Orbit.Core.Tasks.ShareTaskList;

/// <summary>Mirrors Orbit.Core.Notes.ShareNote.ShareNoteCommandHandler - see its class comment for the permission rules enforced here.</summary>
public sealed class ShareTaskListCommandHandler : IRequestHandler<ShareTaskListCommand, ShareOutcome?>
{
    private readonly TaskListAccessResolver _taskListAccessResolver;
    private readonly ITaskListShareRepository _taskListShareRepository;
    private readonly ISharedItemNotifier _sharedItemNotifier;

    public ShareTaskListCommandHandler(TaskListAccessResolver taskListAccessResolver, ITaskListShareRepository taskListShareRepository, ISharedItemNotifier sharedItemNotifier)
    {
        _taskListAccessResolver = taskListAccessResolver;
        _taskListShareRepository = taskListShareRepository;
        _sharedItemNotifier = sharedItemNotifier;
    }

    public async Task<ShareOutcome?> HandleAsync(ShareTaskListCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskListAccessResolver.ResolveAsync(request.OwnerUserId, request.TaskListId, cancellationToken);
        if (taskList is null)
        {
            return null;
        }

        if (taskList.IsPrivate)
        {
            // A private task list has no readable content on the server and is the owner's alone by
            // definition - refused here as well as hidden in the client, so a hand-made request can't
            // create a share that would only ever hand someone ciphertext they cannot open.
            throw new InvalidRequestException("A private task list can't be shared.");
        }

        if (request.RecipientUserId == taskList.UserId)
        {
            return null;
        }

        if (taskList.IsShared && (taskList.AccessLevel < ShareAccessLevel.Share || request.AccessLevel > taskList.AccessLevel))
        {
            return null;
        }

        var existingShare = await _taskListShareRepository.FindExistingAsync(taskList.Id, request.RecipientUserId, cancellationToken);
        if (existingShare is not null)
        {
            return new ShareOutcome(existingShare.Id, AlreadyShared: true);
        }

        var share = TaskListShare.Create(taskList.Id, taskList.UserId, request.RecipientUserId, request.AccessLevel);
        await _taskListShareRepository.AddAsync(share, cancellationToken);
        await _sharedItemNotifier.NotifyAsync(
            request.RecipientUserId, request.OwnerUserId, SharedItemKind.TaskList, taskList.Title, cancellationToken);
        return new ShareOutcome(share.Id, AlreadyShared: false);
    }
}
