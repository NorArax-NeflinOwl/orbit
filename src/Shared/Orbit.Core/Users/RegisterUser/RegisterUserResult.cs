namespace Orbit.Core.Users.RegisterUser;

/// <summary>
/// Outcome of <see cref="RegisterUserCommand"/>: either the newly created user, or the reason
/// registration was rejected (currently a taken email address or a taken username).
/// </summary>
public sealed class RegisterUserResult
{
    public User? User { get; }
    public string? Error { get; }

    private RegisterUserResult(User? user, string? error)
    {
        User = user;
        Error = error;
    }

    public static RegisterUserResult Success(User user) => new(user, null);

    public static RegisterUserResult Failure(string error) => new(null, error);
}
