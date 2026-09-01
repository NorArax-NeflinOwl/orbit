using Orbit.Core.Abstractions;
using Orbit.Core.LiveUpdates;

namespace Orbit.Core.Chat.SetConversationArchived;

public sealed class SetConversationArchivedCommandHandler
    : IRequestHandler<SetConversationArchivedCommand, bool>
{
    private readonly IContactRepository _contactRepository;
    private readonly ILiveUpdatePublisher _liveUpdatePublisher;

    public SetConversationArchivedCommandHandler(
        IContactRepository contactRepository, ILiveUpdatePublisher liveUpdatePublisher)
    {
        _contactRepository = contactRepository;
        _liveUpdatePublisher = liveUpdatePublisher;
    }

    /// <summary>
    /// False when the caller has no row for that person, which is what an id nobody recognises looks
    /// like from here - the API turns it into a 404.
    /// </summary>
    public async Task<bool> HandleAsync(SetConversationArchivedCommand request, CancellationToken cancellationToken)
    {
        if (!await _contactRepository.SetArchivedAsync(
            request.UserId, request.OtherUserId, request.IsArchived, cancellationToken))
        {
            return false;
        }

        // To this account only, which means its other devices: a conversation put away on a phone
        // should not still be in the way on the laptop. The other party hears nothing, because as far
        // as their list is concerned nothing happened.
        await _liveUpdatePublisher.ChatChangedAsync(request.UserId, cancellationToken);
        return true;
    }
}
