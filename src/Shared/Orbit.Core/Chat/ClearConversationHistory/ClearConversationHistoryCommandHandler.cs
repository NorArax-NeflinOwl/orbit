using Orbit.Core.Abstractions;
using Orbit.Core.LiveUpdates;

namespace Orbit.Core.Chat.ClearConversationHistory;

public sealed class ClearConversationHistoryCommandHandler : IRequestHandler<ClearConversationHistoryCommand, bool>
{
    private readonly IContactRepository _contactRepository;
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly ILiveUpdatePublisher _liveUpdatePublisher;

    public ClearConversationHistoryCommandHandler(
        IContactRepository contactRepository,
        IChatMessageRepository chatMessageRepository,
        ILiveUpdatePublisher liveUpdatePublisher)
    {
        _contactRepository = contactRepository;
        _chatMessageRepository = chatMessageRepository;
        _liveUpdatePublisher = liveUpdatePublisher;
    }

    /// <summary>
    /// False when the caller has no row for that person, which is what an id nobody recognises looks
    /// like from here - the API turns it into a 404.
    /// </summary>
    public async Task<bool> HandleAsync(ClearConversationHistoryCommand request, CancellationToken cancellationToken)
    {
        var clearedAtUtc = DateTimeOffset.UtcNow;
        if (!await _contactRepository.ClearHistoryAsync(request.UserId, request.OtherUserId, clearedAtUtc, cancellationToken))
        {
            return false;
        }

        // Marked read as well as hidden. Somebody who has just emptied a conversation is not going to
        // read what was in it, and leaving a count of unread messages behind would say there is
        // something waiting on a screen that now shows nothing.
        await _chatMessageRepository.MarkConversationAsReadAsync(
            request.UserId, request.OtherUserId, clearedAtUtc, cancellationToken);

        // This account only, which means its other devices. The other party hears nothing, because as
        // far as their conversation is concerned nothing happened.
        await _liveUpdatePublisher.ChatChangedAsync(request.UserId, cancellationToken);
        return true;
    }
}
