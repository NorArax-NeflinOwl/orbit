using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.SetPublicKey;

public sealed class SetPublicKeyCommandHandler : IRequestHandler<SetPublicKeyCommand, bool>
{
    private readonly IUserRepository _userRepository;

    public SetPublicKeyCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <summary>
    /// Returns false instead of throwing when the user id from the (already-validated) JWT no longer
    /// matches an account, so the API can turn that into a 404 rather than an unhandled exception.
    /// </summary>
    public async Task<bool> HandleAsync(SetPublicKeyCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        user.SetPublicKey(request.PublicKeyBase64);
        await _userRepository.UpdateAsync(user, cancellationToken);
        return true;
    }
}
