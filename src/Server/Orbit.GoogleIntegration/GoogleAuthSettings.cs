namespace Orbit.GoogleIntegration;

/// <summary>
/// Configuration for "sign in with Google". Only the client id is needed: Orbit verifies an ID token the
/// browser already obtained, rather than running a server-side code exchange, so there is no client
/// secret to keep here. The id is public by design - it ends up in the page anyway.
/// </summary>
public sealed class GoogleAuthSettings
{
    public const string SectionName = "GoogleAuth";

    /// <summary>Empty when Google sign-in isn't set up for this deployment; everything then behaves as if the feature didn't exist.</summary>
    public string ClientId { get; set; } = string.Empty;
}
