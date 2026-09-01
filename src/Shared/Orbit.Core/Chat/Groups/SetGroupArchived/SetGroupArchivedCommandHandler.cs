using Orbit.Core.Abstractions;
using Orbit.Core.LiveUpdates;

namespace Orbit.Core.Chat.Groups.SetGroupArchived;

public sealed class SetGroupArchivedCommandHandler : IRequestHandler<SetGroupArchivedCommand, bool>
{
    private readonly IChatGroupRepository _chatGroupRepository;
    private readonly ILiveUpdatePublisher _liveUpdatePublisher;

    public SetGroupArchivedCommandHandler(
        IChatGroupRepository chatGroupRepository, ILiveUpdatePublisher liveUpdatePublisher)
    {
        _chatGroupRepository = chatGroupRepository;
        _liveUpdatePublisher = liveUpdatePublisher;
    }

    /// <summary>
    /// False when the group is gone or the caller is not in it - the API turns both into a 404, which
    /// is the same answer either way: there is no such group as far as this caller is concerned.
    /// </summary>
    public async Task<bool> HandleAsync(SetGroupArchivedCommand request, CancellationToken cancellationToken)
    {
        if (await _chatGroupRepository.GetByIdAsync(request.GroupId, cancellationToken) is not { } group
            || !group.SetArchivedFor(request.UserId, request.IsArchived))
        {
            return false;
        }

        await _chatGroupRepository.UpdateAsync(group, cancellationToken);

        // This account only. The group itself did not change, so telling the other members would be
        // announcing something that is not true of their lists.
        await _liveUpdatePublisher.ChatChangedAsync(request.UserId, cancellationToken);
        return true;
    }
}
