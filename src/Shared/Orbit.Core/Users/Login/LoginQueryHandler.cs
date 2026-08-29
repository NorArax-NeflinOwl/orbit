using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.Login;

/// <summary>
/// Verifies credentials for either an email address or a login, together with a password, and says
/// which of the two was wrong when one of them was.
///
/// That is a deliberate trade. Answering "no such account" separately from "wrong password" makes this
/// endpoint an account-existence oracle: somebody can ask it whether an address has an Orbit account.
/// Orbit accepts that here because registration already answers the same question - it refuses a taken
/// email address by name, and has to, or nobody could tell which of the two fields to change - so
/// keeping login silent about it protected nothing while leaving a reader to guess which half of what
/// they typed was wrong. What still stands between this and a list of Orbit's users is the rate limit on
/// the whole auth group (see RateLimiterPolicyNames.Auth).
///
/// Password reset is the exception and stays silent: it sends mail to an address the caller named, so an
/// answer there would be an oracle anybody could point at anybody - see RequestPasswordResetCommand.
/// </summary>
public sealed class LoginQueryHandler : IRequestHandler<LoginQuery, LoginResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public LoginQueryHandler(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<LoginResult> HandleAsync(LoginQuery request, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(request.EmailOrUserName.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null)
        {
            return LoginResult.Refused(LoginRejection.NoSuchAccount);
        }

        // A Google account that has never set a password has nothing to check a password against, so no
        // password can ever be right for it - refused here rather than handed as null to the hasher.
        if (user.PasswordHash is not { } passwordHash)
        {
            return LoginResult.Refused(LoginRejection.NoPasswordSet);
        }

        return _passwordHasher.Verify(request.Password, passwordHash)
            ? LoginResult.SignedIn(user)
            : LoginResult.Refused(LoginRejection.WrongPassword);
    }

    /// <summary>
    /// Registration enforces that email addresses and logins are both unique, so trying the identifier
    /// as an email first and falling back to a login lookup can never be ambiguous.
    /// </summary>
    private async Task<User?> FindUserAsync(string emailOrUserName, CancellationToken cancellationToken)
        => await _userRepository.GetByEmailAsync(emailOrUserName, cancellationToken)
            ?? await _userRepository.GetByUserNameAsync(emailOrUserName, cancellationToken);
}
