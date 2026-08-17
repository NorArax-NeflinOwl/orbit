using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.GetConversation;

public sealed class GetConversationQueryHandler : IRequestHandler<GetConversationQuery, IReadOnlyList<ChatMessage>>
{
    private readonly IChatMessageRepository _chatMessageRepository;

    public GetConversationQueryHandler(IChatMessageRepository chatMessageRepository)
    {
        _chatMessageRepository = chatMessageRepository;
    }

    public Task<IReadOnlyList<ChatMessage>> HandleAsync(GetConversationQuery request, CancellationToken cancellationToken)
        => _chatMessageRepository.GetConversationAsync(request.UserId, request.OtherUserId, request.SinceUtc, cancellationToken);
}
