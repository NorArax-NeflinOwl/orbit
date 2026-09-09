using Orbit.Core.Abstractions;
using Orbit.Core.Chat.Groups;
using Orbit.Core.LiveUpdates;

namespace Orbit.Core.Chat.DeleteMessage;

/// <summary>
/// Deletes a message for everyone, not just for the person asking - there is one row per recipient and
/// removing only your own copy would leave the message standing for everybody else, which is not what
/// "delete" is taken to mean anywhere in this app.
///
/// The row stays and its words go (see ChatMessage.Delete), so the conversation says a message was here
/// and was taken back. Removing the row outright left a hole: the other person's screen simply had one
/// fewer line than a moment ago, with nothing saying why, which reads as a bug or as never having been
/// sent. What "deleted" means is unchanged - the ciphertext is emptied, so there is nothing left to
/// read whatever any client chooses to draw.
///
/// Who may: the sender, always. In a group, an admin as well, for anyone's message (see
/// ChatGroup.CanDeleteMessageFrom). Nobody else, including a recipient of a one-to-one message - being
/// sent something doesn't give you the right to erase it from the sender's own history.
/// </summary>
public sealed class DeleteChatMessageCommandHandler : IRequestHandler<DeleteChatMessageCommand, bool>
{
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly IChatGroupRepository _chatGroupRepository;
    private readonly ILiveUpdatePublisher _liveUpdatePublisher;

    public DeleteChatMessageCommandHandler(
        IChatMessageRepository chatMessageRepository,
        IChatGroupRepository chatGroupRepository,
        ILiveUpdatePublisher liveUpdatePublisher)
    {
        _chatMessageRepository = chatMessageRepository;
        _chatGroupRepository = chatGroupRepository;
        _liveUpdatePublisher = liveUpdatePublisher;
    }

    public async Task<bool> HandleAsync(DeleteChatMessageCommand request, CancellationToken cancellationToken)
    {
        var message = await _chatMessageRepository.GetByIdAsync(request.MessageId, cancellationToken);
        if (message is null)
        {
            return false;
        }

        if (message.GroupId is not { } groupId)
        {
            if (message.SenderUserId != request.ActorUserId)
            {
                return false;
            }

            await _chatMessageRepository.MarkDeletedAsync(
                message.Id, request.ActorUserId, DateTimeOffset.UtcNow, cancellationToken);

            // A message that vanishes is news for whoever was looking at it, which is the recipient
            // first of all - a deletion nobody hears about stays on their screen until the slow poll.
            await _liveUpdatePublisher.ChatChangedAsync(
                [message.RecipientUserId, message.SenderUserId], cancellationToken);
            return true;
        }

        var group = await _chatGroupRepository.GetByIdAsync(groupId, cancellationToken);
        if (group is null || !group.CanDeleteMessageFrom(request.ActorUserId, message.SenderUserId))
        {
            return false;
        }

        // Every copy of the same posting goes, so the message leaves the group rather than one member's
        // view of it - see ChatMessage.GroupMessageId.
        await _chatMessageRepository.MarkGroupMessageDeletedAsync(
            message.GroupMessageId!.Value, request.ActorUserId, DateTimeOffset.UtcNow, cancellationToken);
        await _liveUpdatePublisher.ChatChangedAsync(
            [.. group.Members.Select(member => member.UserId)], cancellationToken);
        return true;
    }
}
