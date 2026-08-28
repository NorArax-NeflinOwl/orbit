using Orbit.Core.Abstractions;

namespace Orbit.Core.Users.Login;

[ClientAction(ClientActionCategory.Login)]
public sealed record LoginQuery(string EmailOrUserName, string Password) : IRequest<LoginResult>;

/// <summary>Why a sign-in was refused, when it was.</summary>
public enum LoginRejection
{
    /// <summary>It wasn't - see <see cref="LoginResult.User"/>.</summary>
    None,

    /// <summary>No account answers to that email address or login.</summary>
    NoSuchAccount,

    /// <summary>The account is there; the password is not its password.</summary>
    WrongPassword,

    /// <summary>
    /// The account is there and has no password at all - it was made with Google and has never set one,
    /// so no password can ever be right for it. Said out loud rather than reported as a wrong password,
    /// which would send somebody looking for a password that does not exist.
    /// </summary>
    NoPasswordSet
}

/// <summary>
/// Who signed in, or why nobody did. The two travel together because a refusal is an answer to the same
/// question - see <see cref="LoginQueryHandler"/> for what telling the reasons apart costs.
/// </summary>
public sealed record LoginResult(User? User, LoginRejection Rejection)
{
    public static LoginResult SignedIn(User user) => new(user, LoginRejection.None);

    public static LoginResult Refused(LoginRejection rejection) => new(null, rejection);
}
