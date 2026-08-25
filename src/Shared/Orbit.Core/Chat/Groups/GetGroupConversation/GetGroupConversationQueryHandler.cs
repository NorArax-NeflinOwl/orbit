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
    /// Only the copies this caller can actually decrypt: the ones addressed to them, plus the ones they
    /// sent (encrypted under a pairwise key they hold either way). Someone else's copy would be
    /// unreadable ciphertext to them, and sending it would leak nothing but noise.
    /// </summary>
    public async Task<IReadOnlyList<ChatMessage>> HandleAsync(GetGroupConversationQuery request, CancellationToken cancellationToken)
    {
        var group = await _chatGroupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group is null || !group.IsMember(request.UserId))
        {
            return [];
        }

        return await _chatMessageRepository.GetGroupConversationAsync(request.GroupId, request.UserId, cancellationToken);
    }
}
