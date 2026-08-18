using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.GetConversationAccess;

public sealed class GetConversationAccessQueryHandler : IRequestHandler<GetConversationAccessQuery, ChatConversationAccess?>
{
    private readonly IChatConversationAccessRepository _chatConversationAccessRepository;

    public GetConversationAccessQueryHandler(IChatConversationAccessRepository chatConversationAccessRepository)
    {
        _chatConversationAccessRepository = chatConversationAccessRepository;
    }

    public Task<ChatConversationAccess?> HandleAsync(GetConversationAccessQuery request, CancellationToken cancellationToken)
        => _chatConversationAccessRepository.GetAsync(request.UserId, request.OtherUserId, cancellationToken);
}
