using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.SetPresence;

public sealed class PresenceHeartbeatCommandHandler : IRequestHandler<PresenceHeartbeatCommand, bool>
{
    private readonly IUserRepository _userRepository;

    public PresenceHeartbeatCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<bool> HandleAsync(PresenceHeartbeatCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        user.RecordSeen(DateTimeOffset.UtcNow);
        await _userRepository.UpdateAsync(user, cancellationToken);
        return true;
    }
}
