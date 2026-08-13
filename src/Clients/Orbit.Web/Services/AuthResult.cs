namespace Orbit.Web.Services;

/// <summary>
/// Distinguishes why a login or registration attempt failed, so the UI can show a message tailored to
/// the cause instead of a generic error.
/// </summary>
public enum AuthOutcome
{
    Success,
    InvalidCredentials,
    EmailAlreadyRegistered,
    UnexpectedError
}

/// <summary>
/// Result of a login or registration attempt against Orbit.Api's auth endpoints.
/// </summary>
public sealed class AuthResult
{
    public AuthOutcome Outcome { get; }

    private AuthResult(AuthOutcome outcome)
    {
        Outcome = outcome;
    }

    public static AuthResult Success() => new(AuthOutcome.Success);

    public static AuthResult InvalidCredentials() => new(AuthOutcome.InvalidCredentials);

    public static AuthResult EmailAlreadyRegistered() => new(AuthOutcome.EmailAlreadyRegistered);

    public static AuthResult UnexpectedError() => new(AuthOutcome.UnexpectedError);
}
