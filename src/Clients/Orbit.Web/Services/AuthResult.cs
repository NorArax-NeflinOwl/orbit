namespace Orbit.Web.Services;

/// <summary>
/// Distinguishes why a login or registration attempt failed, so the UI can show a message tailored to
/// the cause instead of a generic error.
/// </summary>
public enum AuthOutcome
{
    Success,
    InvalidCredentials,
    // The API reports a taken email address and a taken username identically (409, message-only body),
    // so the client can't tell them apart without parsing that message - this covers both.
    EmailOrUserNameAlreadyTaken,
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

    public static AuthResult EmailOrUserNameAlreadyTaken() => new(AuthOutcome.EmailOrUserNameAlreadyTaken);

    public static AuthResult UnexpectedError() => new(AuthOutcome.UnexpectedError);
}
