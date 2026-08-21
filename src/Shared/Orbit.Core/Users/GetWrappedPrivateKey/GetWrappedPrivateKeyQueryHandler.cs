using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.GetWrappedPrivateKey;

public sealed class GetWrappedPrivateKeyQueryHandler : IRequestHandler<GetWrappedPrivateKeyQuery, WrappedPrivateKey?>
{
    private readonly IUserRepository _userRepository;

    public GetWrappedPrivateKeyQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <summary>
    /// Null covers two cases the caller doesn't need to tell apart: the user id from the JWT no longer
    /// matches an account, or it does but this is the first login since the account either was created
    /// or last had its browser storage cleared - either way, OwnEncryptionKeyProvider's response is the
    /// same, generate a fresh key pair instead of restoring one.
    /// </summary>
    public async Task<WrappedPrivateKey?> HandleAsync(GetWrappedPrivateKeyQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        return user?.WrappedPrivateKey;
    }
}
