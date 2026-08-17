using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.MarkConversationAsRead;

public sealed class MarkConversationAsReadCommandHandler : IRequestHandler<MarkConversationAsReadCommand, bool>
{
    private readonly IChatMessageRepository _chatMessageRepository;

    public MarkConversationAsReadCommandHandler(IChatMessageRepository chatMessageRepository)
    {
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
        return true;
    }
}
