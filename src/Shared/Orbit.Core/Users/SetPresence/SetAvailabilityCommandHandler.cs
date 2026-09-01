using Orbit.Core.Abstractions;
using Orbit.Core.Chat;
using Orbit.Core.LiveUpdates;

namespace Orbit.Core.Users.SetPresence;

public sealed class SetAvailabilityCommandHandler : IRequestHandler<SetAvailabilityCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IContactRepository _contactRepository;
    private readonly ILiveUpdatePublisher _liveUpdatePublisher;

    public SetAvailabilityCommandHandler(
        IUserRepository userRepository, IContactRepository contactRepository, ILiveUpdatePublisher liveUpdatePublisher)
    {
        _userRepository = userRepository;
        _contactRepository = contactRepository;
        _liveUpdatePublisher = liveUpdatePublisher;
    }

    public async Task<bool> HandleAsync(SetAvailabilityCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        user.SetAvailability(request.Availability, DateTimeOffset.UtcNow);
        await _userRepository.UpdateAsync(user, cancellationToken);

        // Unconditional, unlike the heartbeat: somebody chose this, so it is news even when the status
        // it resolves to happens to be the one already showing.
        var contacts = await _contactRepository.GetAllForUserAsync(user.Id, cancellationToken);
        await _liveUpdatePublisher.PresenceChangedAsync(
            user.Id, [.. contacts.Select(contact => contact.ContactUserId)], cancellationToken);
        return true;
    }
}
