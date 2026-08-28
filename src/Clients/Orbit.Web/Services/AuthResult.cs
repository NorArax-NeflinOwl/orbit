namespace Orbit.Web.Services;

/// <summary>
/// Distinguishes why a login or registration attempt failed, so the UI can show a message tailored to
/// the cause instead of a generic error.
/// </summary>
public enum AuthOutcome
{
    Success,

    /// <summary>Refused, without the server saying which half was wrong - what a Google sign-in gets.</summary>
    InvalidCredentials,

    // Told apart for the same reason the registration refusals below are: a reader who is not told
    // which of the two fields to change has to guess at both (see LoginRejection).
    NoSuchAccount,
    WrongPassword,

    /// <summary>The account signs in with Google and has never set a password.</summary>
    PasswordNotSet,

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

    public static AuthResult Refused(AuthOutcome outcome) => new(outcome);

    public static AuthResult EmailAlreadyTaken() => new(AuthOutcome.EmailAlreadyTaken);

    public static AuthResult UserNameAlreadyTaken() => new(AuthOutcome.UserNameAlreadyTaken);

    public static AuthResult UnexpectedError() => new(AuthOutcome.UnexpectedError);
}
