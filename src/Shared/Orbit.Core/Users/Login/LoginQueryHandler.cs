using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.Login;

/// <summary>
/// Verifies credentials for either an email address or a username, together with a password. Returns
/// null for an unknown identifier and for a wrong password alike, so a failed login can't be used to
/// discover which email addresses or usernames have an account.
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
        var user = await FindUserAsync(request.EmailOrUserName.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null)
        {
            return null;
        }

        return _passwordHasher.Verify(request.Password, user.PasswordHash) ? user : null;
    }

    /// <summary>
    /// Registration enforces that email addresses and usernames are both unique, so trying the
    /// identifier as an email first and falling back to a username lookup can never be ambiguous.
    /// </summary>
    private async Task<User?> FindUserAsync(string emailOrUserName, CancellationToken cancellationToken)
        => await _userRepository.GetByEmailAsync(emailOrUserName, cancellationToken)
            ?? await _userRepository.GetByUserNameAsync(emailOrUserName, cancellationToken);
}
