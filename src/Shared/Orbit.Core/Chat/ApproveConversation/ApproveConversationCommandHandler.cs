using Orbit.Core.Abstractions;
using Orbit.Core.LiveUpdates;

namespace Orbit.Core.Chat.ApproveConversation;

public sealed class ApproveConversationCommandHandler : IRequestHandler<ApproveConversationCommand, bool>
{
    private readonly IChatConversationAccessRepository _chatConversationAccessRepository;
    private readonly ILiveUpdatePublisher _liveUpdatePublisher;

    public ApproveConversationCommandHandler(
        IChatConversationAccessRepository chatConversationAccessRepository, ILiveUpdatePublisher liveUpdatePublisher)
    {
        _chatConversationAccessRepository = chatConversationAccessRepository;
        _liveUpdatePublisher = liveUpdatePublisher;
    }

    public async Task<bool> HandleAsync(ApproveConversationCommand request, CancellationToken cancellationToken)
    {
        if (!await _chatConversationAccessRepository.ApproveAsync(
            request.ApprovingUserId, request.OtherUserId, cancellationToken))
        {
            return false;
        }

        // The other party is the one waiting on this: their compose box is disabled until they hear it.
        await _liveUpdatePublisher.ChatChangedAsync(
            [request.OtherUserId, request.ApprovingUserId], cancellationToken);
        return true;
    }
}
