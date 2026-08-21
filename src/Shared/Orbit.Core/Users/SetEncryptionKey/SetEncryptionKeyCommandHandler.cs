using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.SetEncryptionKey;

public sealed class SetEncryptionKeyCommandHandler : IRequestHandler<SetEncryptionKeyCommand, bool>
{
    private readonly IUserRepository _userRepository;

    public SetEncryptionKeyCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <summary>
    /// Returns false instead of throwing when the user id from the (already-validated) JWT no longer
    /// matches an account, so the API can turn that into a 404 rather than an unhandled exception - see
    /// SetPublicKeyCommandHandler for the same reasoning.
    /// </summary>
    public async Task<bool> HandleAsync(SetEncryptionKeyCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        user.SetEncryptionKey(request.PublicKeyBase64, request.WrappedPrivateKey);
        await _userRepository.UpdateAsync(user, cancellationToken);
        return true;
    }
}
