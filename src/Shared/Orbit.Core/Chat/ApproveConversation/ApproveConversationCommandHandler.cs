using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.ApproveConversation;

public sealed class ApproveConversationCommandHandler : IRequestHandler<ApproveConversationCommand, bool>
{
    private readonly IChatConversationAccessRepository _chatConversationAccessRepository;

    public ApproveConversationCommandHandler(IChatConversationAccessRepository chatConversationAccessRepository)
    {
        _chatConversationAccessRepository = chatConversationAccessRepository;
    }

    public Task<bool> HandleAsync(ApproveConversationCommand request, CancellationToken cancellationToken)
        => _chatConversationAccessRepository.ApproveAsync(request.ApprovingUserId, request.OtherUserId, cancellationToken);
}
