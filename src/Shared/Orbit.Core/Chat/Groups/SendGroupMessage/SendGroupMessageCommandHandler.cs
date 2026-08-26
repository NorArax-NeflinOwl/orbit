using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.Groups.SendGroupMessage;

public sealed class SendGroupMessageCommandHandler : IRequestHandler<SendGroupMessageCommand, bool>
{
    private readonly IChatGroupRepository _chatGroupRepository;
    private readonly IChatMessageRepository _chatMessageRepository;

    public SendGroupMessageCommandHandler(IChatGroupRepository chatGroupRepository, IChatMessageRepository chatMessageRepository)
    {
        _chatGroupRepository = chatGroupRepository;
        _chatMessageRepository = chatMessageRepository;
    }

    public async Task<bool> HandleAsync(SendGroupMessageCommand request, CancellationToken cancellationToken)
    {
        var group = await _chatGroupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group is null || !group.IsMember(request.SenderUserId))
        {
            return false;
        }

        // Every copy has to be addressed to a current member, and every current member other than the
        // sender has to get one. Checked rather than trusted because the client decides the fan-out:
        // a missing copy would silently cut someone out of a conversation they are in, and an extra one
        // would deliver into a group the recipient has no part in.
        var expectedRecipients = group.Members
            .Select(member => member.UserId)
            .Where(userId => userId != request.SenderUserId)
            .ToHashSet();
        var addressed = request.Copies.Select(copy => copy.RecipientUserId).ToHashSet();

        if (!expectedRecipients.SetEquals(addressed))
        {
            throw new InvalidRequestException(
                "A group message needs exactly one copy for each other member - the group's membership changed, so reload it and send again.");
        }

        var groupMessageId = Guid.NewGuid();
        // One instant for the whole fan-out, not one per copy - see ChatMessage.CreateForGroup.
        var sentAtUtc = DateTimeOffset.UtcNow;
        foreach (var copy in request.Copies)
        {
            await _chatMessageRepository.AddAsync(
                ChatMessage.CreateForGroup(
                    group.Id, groupMessageId, request.SenderUserId, copy.RecipientUserId, copy.CiphertextBase64, copy.NonceBase64,
                    sentAtUtc),
                cancellationToken);
        }

        return true;
    }
}
