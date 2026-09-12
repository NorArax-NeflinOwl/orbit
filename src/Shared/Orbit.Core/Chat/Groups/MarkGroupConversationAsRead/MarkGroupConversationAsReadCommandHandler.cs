using Orbit.Core.Abstractions;
using Orbit.Core.LiveUpdates;

namespace Orbit.Core.Chat.Groups.MarkGroupConversationAsRead;

public sealed class MarkGroupConversationAsReadCommandHandler
    : IRequestHandler<MarkGroupConversationAsReadCommand, bool>
{
    private readonly IChatGroupRepository _chatGroupRepository;
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly ILiveUpdatePublisher _liveUpdatePublisher;

    public MarkGroupConversationAsReadCommandHandler(
        IChatGroupRepository chatGroupRepository,
        IChatMessageRepository chatMessageRepository,
        ILiveUpdatePublisher liveUpdatePublisher)
    {
        _liveUpdatePublisher = liveUpdatePublisher;
        _chatGroupRepository = chatGroupRepository;
        _chatMessageRepository = chatMessageRepository;
    }

    /// <summary>
    /// Only a member may mark a group read, and only their own copies are touched - reading is something
    /// you do, not something you can do on somebody else's behalf.
    /// </summary>
    public async Task<bool> HandleAsync(MarkGroupConversationAsReadCommand request, CancellationToken cancellationToken)
    {
        var group = await _chatGroupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group is null || !group.IsMember(request.ReaderUserId))
        {
            return false;
        }

        var anythingWasUnread = await _chatMessageRepository.MarkGroupConversationAsReadAsync(
            request.ReaderUserId, request.GroupId, DateTimeOffset.UtcNow, request.ReadUpToUtc, cancellationToken);

        // Everyone else in the group, who are the ones showing receipts for what they sent - and only
        // when something was actually read. A read that did not happen, announced, is news the other
        // windows answer by polling and marking read, which announces again: see
        // MarkConversationAsReadCommandHandler, where the same shape ran for a minute at sixteen
        // requests a second between two people. In a group it would be everybody at once.
        if (anythingWasUnread)
        {
            await _liveUpdatePublisher.ChatChangedAsync(
                [.. group.Members.Select(member => member.UserId).Where(userId => userId != request.ReaderUserId)],
                cancellationToken);
        }

        return true;
    }
}
