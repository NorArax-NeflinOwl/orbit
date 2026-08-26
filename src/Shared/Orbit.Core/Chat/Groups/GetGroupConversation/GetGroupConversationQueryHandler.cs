namespace Orbit.Core.Chat.Groups.GetGroupConversation;

using Orbit.Core.Abstractions;

public sealed class GetGroupConversationQueryHandler : IRequestHandler<GetGroupConversationQuery, IReadOnlyList<ChatMessage>>
{
    private readonly IChatGroupRepository _chatGroupRepository;
    private readonly IChatMessageRepository _chatMessageRepository;

    public GetGroupConversationQueryHandler(IChatGroupRepository chatGroupRepository, IChatMessageRepository chatMessageRepository)
    {
        _chatGroupRepository = chatGroupRepository;
        _chatMessageRepository = chatMessageRepository;
    }

    /// <summary>
    /// One row per message the caller can read, not one per stored copy.
    ///
    /// A group message is encrypted separately for each member, so posting to a group of three stores
    /// two rows - and both of them name the sender. Handing back everything the caller can decrypt
    /// therefore showed the sender their own message once per recipient, which is why the duplication
    /// only appeared past two members. GroupMessageId is what ties the copies together; the caller gets
    /// one of each, and someone else's copy - unreadable ciphertext to them - is left out as before.
    /// </summary>
    public async Task<IReadOnlyList<ChatMessage>> HandleAsync(GetGroupConversationQuery request, CancellationToken cancellationToken)
    {
        var group = await _chatGroupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group is null || !group.IsMember(request.UserId))
        {
            return [];
        }

        var messages = await _chatMessageRepository.GetGroupConversationAsync(request.GroupId, request.UserId, cancellationToken);

        // Ordered by id within a group of copies, so the same one is chosen on every read: the browser
        // caches decrypted text against the copy's id, and a choice that wandered between polls would
        // throw that cache away each time.
        return messages
            .GroupBy(message => message.GroupMessageId ?? message.Id)
            .Select(copies => copies.OrderBy(copy => copy.Id).First())
            .OrderBy(message => message.SentAtUtc)
            .ToList();
    }
}
