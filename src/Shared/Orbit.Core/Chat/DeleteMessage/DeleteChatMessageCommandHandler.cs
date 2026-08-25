using Orbit.Core.Abstractions;
using Orbit.Core.Chat.Groups;

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

    public DeleteChatMessageCommandHandler(IChatMessageRepository chatMessageRepository, IChatGroupRepository chatGroupRepository)
    {
        _chatMessageRepository = chatMessageRepository;
        _chatGroupRepository = chatGroupRepository;
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
            return true;
        }

        var group = await _chatGroupRepository.GetByIdAsync(groupId, cancellationToken);
        if (group is null || !group.CanDeleteMessageFrom(request.ActorUserId, message.SenderUserId))
        {
            return false;
        }

        // Every copy of the same posting goes, so the message leaves the group rather than one member's
        // view of it - see ChatMessage.GroupMessageId.
        await _chatMessageRepository.DeleteGroupMessageAsync(message.GroupMessageId!.Value, cancellationToken);
        return true;
    }
}
