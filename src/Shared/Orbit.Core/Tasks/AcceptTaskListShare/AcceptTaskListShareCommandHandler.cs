using Orbit.Core.Abstractions;
using Orbit.Core.Users;

namespace Orbit.Core.Tasks.AcceptTaskListShare;

public sealed class AcceptTaskListShareCommandHandler : IRequestHandler<AcceptTaskListShareCommand, bool>
{
    private readonly ITaskListShareRepository _taskListShareRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IUserRepository _userRepository;

    public AcceptTaskListShareCommandHandler(
        ITaskListShareRepository taskListShareRepository, ITaskRepository taskRepository, IUserRepository userRepository)
    {
        _taskListShareRepository = taskListShareRepository;
        _taskRepository = taskRepository;
        _userRepository = userRepository;
    }

    public async Task<bool> HandleAsync(AcceptTaskListShareCommand request, CancellationToken cancellationToken)
    {
        var share = await _taskListShareRepository.GetByIdAsync(request.RecipientUserId, request.ShareId, cancellationToken);
        if (share is null)
        {
            return false;
        }

        // Already accepted - report success without creating a second copy, so a duplicate click is harmless.
        if (share.IsAccepted)
        {
            return true;
        }

        var sourceTaskList = await _taskRepository.GetByIdAsync(share.OwnerUserId, share.SourceTaskListId, cancellationToken);
        var owner = await _userRepository.GetByIdAsync(share.OwnerUserId, cancellationToken);
        if (sourceTaskList is null || owner is null)
        {
            return false;
        }

        // Items are copied as-is, including completion state - the recipient's copy starts as a
        // snapshot of the owner's list at share-acceptance time, same as a shared calendar event's
        // details are a snapshot rather than a live reference.
        var sharedTaskList = TaskList.CreateShared(
            request.RecipientUserId, sourceTaskList.Title, sourceTaskList.Items, owner.UserName, share.AccessLevel);
        await _taskRepository.AddAsync(sharedTaskList, cancellationToken);

        share.MarkAccepted(sharedTaskList.Id);
        await _taskListShareRepository.UpdateAsync(share, cancellationToken);
        return true;
    }
}
