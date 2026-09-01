using Orbit.Core.Abstractions;
using Orbit.Core.Chat.Groups;
using Orbit.Core.LiveUpdates;

namespace Orbit.Core.Chat.DeleteMessage;

/// <summary>
/// Deletes a message for everyone, not just for the person asking - there is one row per recipient and
/// removing only your own copy would leave the message standing for everybody else, which is not what
/// "delete" is taken to mean anywhere in this app.
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
        IChatMessageRepository chatMessageRepository, IChatGroupRepository chatGroupRepository,
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

            await _chatMessageRepository.DeleteAsync(message.Id, cancellationToken);
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
        var copies = await _chatMessageRepository.GetGroupMessageCopiesAsync(
            message.GroupMessageId!.Value, cancellationToken);

        await _chatMessageRepository.DeleteGroupMessageAsync(message.GroupMessageId!.Value, cancellationToken);

        // Read before the delete, because afterwards there is nothing left to say who held a copy. The
        // announcement itself has to come after: a client answering it must not find the message still
        // there and put it straight back on screen.
        await _liveUpdatePublisher.ChatChangedAsync(
            [.. copies.Select(copy => copy.RecipientUserId).Distinct()], cancellationToken);

        return true;
    }
}
