using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.SaveOwnLocation;

public sealed class SaveOwnLocationCommandHandler : IRequestHandler<SaveOwnLocationCommand, bool>
{
    private readonly IUserRepository _userRepository;

    public SaveOwnLocationCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<bool> HandleAsync(SaveOwnLocationCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        user.RecordLocation(request.Location);
        await _userRepository.UpdateAsync(user, cancellationToken);
        return true;
    }
}
