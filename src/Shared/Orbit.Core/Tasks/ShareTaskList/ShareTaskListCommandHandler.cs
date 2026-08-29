using Orbit.Core.Abstractions;

using Orbit.Core.Notifications;

namespace Orbit.Core.Tasks.ShareTaskList;

/// <summary>Mirrors Orbit.Core.Notes.ShareNote.ShareNoteCommandHandler - see its class comment for the permission rules enforced here.</summary>
public sealed class ShareTaskListCommandHandler : IRequestHandler<ShareTaskListCommand, ShareOutcome?>
{
    private readonly TaskListAccessResolver _taskListAccessResolver;
    private readonly ITaskListShareRepository _taskListShareRepository;
    private readonly TaskListShareCascade _taskListShareCascade;
    private readonly ISharedItemNotifier _sharedItemNotifier;

    public ShareTaskListCommandHandler(
        TaskListAccessResolver taskListAccessResolver, ITaskListShareRepository taskListShareRepository,
        TaskListShareCascade taskListShareCascade, ISharedItemNotifier sharedItemNotifier)
    {
        _taskListAccessResolver = taskListAccessResolver;
        _taskListShareRepository = taskListShareRepository;
        _taskListShareCascade = taskListShareCascade;
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

        if (taskList.IsShared && !taskList.AccessLevel.CanGrant(request.AccessLevel))
        {
            return null;
        }

        var existingShare = await _taskListShareRepository.FindExistingAsync(taskList.Id, request.RecipientUserId, cancellationToken);
        if (existingShare is not null)
        {
            // Sharing again at a higher level raises the existing offer rather than being a no-op:
            // that is how an owner answers a request for edit access (see RequestEditAccess), and
            // "share it with them again, but with more" is what they mean by doing it.
            var accessLevelRaised = existingShare.RaiseAccessLevelTo(request.AccessLevel);
            if (accessLevelRaised)
            {
                await _taskListShareRepository.UpdateAsync(existingShare, cancellationToken);
            }

            // Re-run even though the offer itself is unchanged: what this list gathers may have grown
            // since it was first shared, and the recipient is meant to have the whole of it.
            await _taskListShareCascade.GrantAsync(
                taskList.UserId, taskList.Id, request.RecipientUserId, request.AccessLevel,
                acceptImmediately: existingShare.IsAccepted, cancellationToken);
            return new ShareOutcome(existingShare.Id, AlreadyShared: true, accessLevelRaised);
        }

        var share = TaskListShare.Create(taskList.Id, taskList.UserId, request.RecipientUserId, request.AccessLevel);
        await _taskListShareRepository.AddAsync(share, cancellationToken);
        // The lists this one gathers, and the inventory it is measured against, follow the offer rather
        // than being offered one by one: one message to accept, and the whole tree opens behind it.
        await _taskListShareCascade.GrantAsync(
            taskList.UserId, taskList.Id, request.RecipientUserId, request.AccessLevel,
            acceptImmediately: false, cancellationToken);
        await _sharedItemNotifier.NotifyAsync(
            request.RecipientUserId, request.OwnerUserId, SharedItemKind.TaskList, taskList.Title, cancellationToken);
        return new ShareOutcome(share.Id, AlreadyShared: false);
    }
}
