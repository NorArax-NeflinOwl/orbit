using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.VerifyPrivatePin;

public sealed class VerifyPrivatePinQueryHandler : IRequestHandler<VerifyPrivatePinQuery, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public VerifyPrivatePinQueryHandler(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    /// <summary>
    /// True only for the account's own PIN. An account that has set none answers **true**: there is no
    /// door there, and a client that asks anyway should be let through rather than left holding a
    /// question nobody can answer.
    /// </summary>
    public async Task<bool> HandleAsync(VerifyPrivatePinQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        return user.PrivatePinHash is not { } pinHash || _passwordHasher.Verify(request.Pin, pinHash);
    }
}
