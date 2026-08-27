namespace Orbit.Core.Chat.Groups.GetGroupConversation;

using Orbit.Core.Abstractions;

public sealed class GetGroupConversationQueryHandler : IRequestHandler<GetGroupConversationQuery, IReadOnlyList<GroupConversationEntry>>
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
    public async Task<IReadOnlyList<GroupConversationEntry>> HandleAsync(
        GetGroupConversationQuery request, CancellationToken cancellationToken)
    {
        var group = await _chatGroupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group is null || !group.IsMember(request.UserId))
        {
            return [];
        }

        var messages = await _chatMessageRepository.GetGroupConversationAsync(
            request.GroupId, request.UserId, request.SinceUtc, cancellationToken);

        // Ordered by id within a group of copies, so the same one is chosen on every read: the browser
        // caches decrypted text against the copy's id, and a choice that wandered between polls would
        // throw that cache away each time.
        var visible = messages
            .GroupBy(message => message.GroupMessageId ?? message.Id)
            .Select(copies => copies.OrderBy(copy => copy.Id).First())
            .OrderBy(message => message.SentAtUtc)
            .ToList();

        // Asked once for the whole page rather than per message: the conversation needs this for every
        // message it draws, and a query each would be the N+1 this codebase keeps finding.
        var ownGroupMessageIds = visible
            .Where(message => message.SenderUserId == request.UserId && message.GroupMessageId is not null)
            .Select(message => message.GroupMessageId!.Value)
            .ToList();
        var receipts = await _chatMessageRepository.GetGroupReceiptsAsync(ownGroupMessageIds, cancellationToken);

        return visible
            .Select(message => new GroupConversationEntry(message, ReadByEveryone(message, request.UserId, receipts)))
            .ToList();
    }

    /// <summary>
    /// Whether every copy of the reader's own message has been read. Null for anybody else's, and for
    /// one with no receipts at all - a group of one has nobody to have read it, and answering "yes"
    /// there would show two ticks for a message nobody received.
    /// </summary>
    private static bool? ReadByEveryone(
        ChatMessage message, Guid readerUserId,
        IReadOnlyDictionary<Guid, IReadOnlyList<GroupMessageReceipt>> receipts)
    {
        if (message.SenderUserId != readerUserId || message.GroupMessageId is not { } groupMessageId)
        {
            return null;
        }

        var copies = receipts.GetValueOrDefault(groupMessageId, []);
        return copies.Count == 0 ? null : copies.All(copy => copy.IsRead);
    }
}
