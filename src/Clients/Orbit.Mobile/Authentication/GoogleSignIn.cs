namespace Orbit.Mobile.Authentication;

/// <summary>How asking Google to identify the holder ended.</summary>
public enum GoogleSignInOutcome
{
    /// <summary>Google issued an identity token for Orbit to verify.</summary>
    Succeeded,

    /// <summary>The reader closed the sheet. Not an error, and not worth a message.</summary>
    Cancelled,

    /// <summary>
    /// This build has no Google client id, so there is nothing to ask with. The button is hidden in
    /// that case rather than offered and made to fail - the same rule Orbit.Web applies when the
    /// server sends no client id (see ClientFlagsDto).
    /// </summary>
    NotConfigured,

    /// <summary>Google was reachable and said no, or the exchange failed.</summary>
    Failed
}

/// <param name="IdToken">The token to hand to Orbit, present only when the outcome is Succeeded.</param>
public sealed record GoogleSignInResult(GoogleSignInOutcome Outcome, string? IdToken = null)
{
    public static GoogleSignInResult Cancelled { get; } = new(GoogleSignInOutcome.Cancelled);

    public static GoogleSignInResult NotConfigured { get; } = new(GoogleSignInOutcome.NotConfigured);

    public static GoogleSignInResult Failed { get; } = new(GoogleSignInOutcome.Failed);

    public static GoogleSignInResult Succeeded(string idToken) => new(GoogleSignInOutcome.Succeeded, idToken);
}

/// <summary>
/// Asking Google who is holding the phone, and coming back with an identity token Orbit's server can
/// verify (<c>POST /api/auth/google</c>, see SignInWithGoogleCommandHandler).
///
/// Behind an interface for the same reason as the other platform seams: it opens a system browser sheet
/// and waits for a redirect back into the app, none of which a test can do. What a test can check is
/// everything around it - that a cancelled sheet says nothing, that a refusal says something, and that
/// a token is actually sent on.
/// </summary>
public interface IGoogleSignIn
{
    /// <summary>
    /// False when this build was given no client id. The sign-in screen hides the button rather than
    /// offering one that could only ever fail - see <see cref="GoogleSignInOutcome.NotConfigured"/>.
    /// </summary>
    bool IsConfigured { get; }

    Task<GoogleSignInResult> RequestIdTokenAsync(CancellationToken cancellationToken = default);
}
