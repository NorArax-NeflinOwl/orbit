namespace Orbit.Web.Services;

/// <summary>
/// Distinguishes why a login or registration attempt failed, so the UI can show a message tailored to
/// the cause instead of a generic error.
/// </summary>
public enum AuthOutcome
{
    Success,
    InvalidCredentials,
    // Told apart rather than lumped together: refusing a registration is only useful if the reader
    // learns which of the two fields to change (see RegistrationConflictDto).
    EmailAlreadyTaken,
    UserNameAlreadyTaken,
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

    public static AuthResult EmailAlreadyTaken() => new(AuthOutcome.EmailAlreadyTaken);

    public static AuthResult UserNameAlreadyTaken() => new(AuthOutcome.UserNameAlreadyTaken);

    public static AuthResult UnexpectedError() => new(AuthOutcome.UnexpectedError);
}
