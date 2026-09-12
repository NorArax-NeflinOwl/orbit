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
        var anythingWasUnread = await _chatMessageRepository.MarkConversationAsReadAsync(
            request.ReaderUserId, request.OtherUserId, DateTimeOffset.UtcNow, request.ReadUpToUtc, cancellationToken);

        // The read receipt is the other party's news, not the reader's - without this the tick that
        // says "seen" would be the one thing still waiting on a poll.
        //
        // Only when something was actually read, which is not a saving but the thing that makes this
        // terminate. An open chat window marks the conversation read on every poll and on every
        // announcement it hears; announcing a read that did not happen means the other window hears
        // news, polls, marks read, announces back - and two open windows do that to each other for as
        // long as they are both open. That is what happened on 2026-09-05: four calls, sixteen times a
        // second, 4,332 requests from one caller in a minute, all against two ids.
        if (anythingWasUnread)
        {
            await _liveUpdatePublisher.ChatChangedAsync(request.OtherUserId, cancellationToken);
        }

        return true;
    }
}
