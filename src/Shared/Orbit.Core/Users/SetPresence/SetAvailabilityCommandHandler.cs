using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.SetPresence;

public sealed class SetAvailabilityCommandHandler : IRequestHandler<SetAvailabilityCommand, bool>
{
    private readonly IUserRepository _userRepository;

    public SetAvailabilityCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
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
        return true;
    }
}
