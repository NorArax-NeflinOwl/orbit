using Orbit.Core.Abstractions;
using Orbit.Core.Chat;
using Orbit.Core.LiveUpdates;

namespace Orbit.Core.Users.SetPresence;

public sealed class PresenceHeartbeatCommandHandler : IRequestHandler<PresenceHeartbeatCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IContactRepository _contactRepository;
    private readonly ILiveUpdatePublisher _liveUpdatePublisher;

    public PresenceHeartbeatCommandHandler(
        IUserRepository userRepository, IContactRepository contactRepository, ILiveUpdatePublisher liveUpdatePublisher)
    {
        _userRepository = userRepository;
        _contactRepository = contactRepository;
        _liveUpdatePublisher = liveUpdatePublisher;
    }

    public async Task<bool> HandleAsync(PresenceHeartbeatCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        var nowUtc = DateTimeOffset.UtcNow;

        // Compared before and after rather than announced on every beat. A heartbeat arrives every
        // twenty seconds per open tab and almost all of them say what everyone already believes; only
        // the one that turns somebody from away or offline back into available is news, and announcing
        // the rest would be a broadcast storm carrying no information.
        //
        // Note this can only ever catch somebody *arriving*. Going away happens by time passing, with
        // nothing calling anything, so there is no moment at which the server could announce it - see
        // UserPresence.StatusAt. That transition is still the fallback poll's job, and the reason the
        // clients keep one.
        var statusBefore = user.Presence.StatusAt(nowUtc);
        user.RecordSeen(nowUtc);
        await _userRepository.UpdateAsync(user, cancellationToken);

        if (user.Presence.StatusAt(nowUtc) != statusBefore)
        {
            await AnnounceToContactsAsync(user.Id, cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Only the people this account actually chats with. Presence is visible between contacts, so
    /// broadcasting it wider would tell accounts something they have no way to see in the app.
    /// </summary>
    private async Task AnnounceToContactsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var contacts = await _contactRepository.GetAllForUserAsync(userId, cancellationToken);
        await _liveUpdatePublisher.PresenceChangedAsync(
            userId, [.. contacts.Select(contact => contact.ContactUserId)], cancellationToken);
    }
}
