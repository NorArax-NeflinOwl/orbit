using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.Login;

/// <summary>
/// Verifies email/password credentials. Returns null for both an unknown email and a wrong password,
/// so a failed login can't be used to discover which email addresses have an account.
/// </summary>
public sealed class LoginQueryHandler : IRequestHandler<LoginQuery, User?>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public LoginQueryHandler(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<User?> HandleAsync(LoginQuery request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (user is null)
        {
            return null;
        }

        return _passwordHasher.Verify(request.Password, user.PasswordHash) ? user : null;
    }
}
