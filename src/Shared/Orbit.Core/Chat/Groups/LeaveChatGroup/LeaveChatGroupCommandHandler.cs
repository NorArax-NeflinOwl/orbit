using Orbit.Core.Abstractions;
using Orbit.Core.LiveUpdates;

namespace Orbit.Core.Chat.Groups.LeaveChatGroup;

public sealed class LeaveChatGroupCommandHandler : IRequestHandler<LeaveChatGroupCommand, bool>
{
    private readonly IChatGroupRepository _chatGroupRepository;
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly ILiveUpdatePublisher _liveUpdatePublisher;

    public LeaveChatGroupCommandHandler(
        IChatGroupRepository chatGroupRepository,
        IChatMessageRepository chatMessageRepository,
        ILiveUpdatePublisher liveUpdatePublisher)
    {
        _chatGroupRepository = chatGroupRepository;
        _chatMessageRepository = chatMessageRepository;
        _liveUpdatePublisher = liveUpdatePublisher;
    }

    public async Task<bool> HandleAsync(LeaveChatGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await _chatGroupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group is null || !group.IsMember(request.UserId))
        {
            return false;
        }

        // Removing yourself is allowed without being an admin; the sole admin of a group with other
        // people in it is still refused, and says so - see ChatGroup.RemoveMember.
        group.RemoveMember(request.UserId, request.UserId);

        // Told to whoever is left as well as to the person leaving: their own list has lost a group,
        // and everyone else's member list has changed.
        var toTell = group.Members.Select(member => member.UserId).Append(request.UserId).Distinct().ToList();

        // Before the group itself may be deleted below, so the copies are gone either way.
        await _chatMessageRepository.DeleteGroupCopiesForAsync(request.GroupId, request.UserId, cancellationToken);

        // The last person out empties the group, and an empty group is not something to keep - the same
        // tidy-up RemoveChatGroupMemberCommandHandler does, for the same reason.
        if (group.IsEmpty)
        {
            await _chatGroupRepository.DeleteAsync(group.Id, cancellationToken);
        }
        else
        {
            await _chatGroupRepository.UpdateAsync(group, cancellationToken);
        }

        await _liveUpdatePublisher.ChatChangedAsync(toTell, cancellationToken);
        return true;
    }
}
