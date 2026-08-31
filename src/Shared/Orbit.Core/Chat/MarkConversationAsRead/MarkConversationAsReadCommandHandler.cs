using Orbit.Core.Abstractions;
using Orbit.Core.LiveUpdates;

namespace Orbit.Core.Chat.MarkConversationAsRead;

public sealed class MarkConversationAsReadCommandHandler : IRequestHandler<MarkConversationAsReadCommand, bool>
{
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly ILiveUpdatePublisher _liveUpdatePublisher;

    public MarkConversationAsReadCommandHandler(
        IChatMessageRepository chatMessageRepository, ILiveUpdatePublisher liveUpdatePublisher)
    {
        _liveUpdatePublisher = liveUpdatePublisher;
        _chatMessageRepository = chatMessageRepository;
    }

    /// <summary>
    /// Always succeeds - marking an already-read (or nonexistent) conversation as read is a harmless
    /// no-op, so there's no failure case worth reporting back to the caller.
    /// </summary>
    public async Task<bool> HandleAsync(MarkConversationAsReadCommand request, CancellationToken cancellationToken)
    {
        await _chatMessageRepository.MarkConversationAsReadAsync(
            request.ReaderUserId, request.OtherUserId, DateTimeOffset.UtcNow, cancellationToken);

        // The read receipt is the other party's news, not the reader's - without this the tick that
        // says "seen" would be the one thing still waiting on a poll.
        await _liveUpdatePublisher.ChatChangedAsync(request.OtherUserId, cancellationToken);
        return true;
    }
}
