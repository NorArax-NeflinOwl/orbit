namespace Orbit.GoogleIntegration;

/// <summary>
/// Configuration for "sign in with Google". Only client ids are needed: Orbit verifies an ID token the
/// client already obtained, rather than running a server-side code exchange, so there is no client
/// secret to keep here. The ids are public by design - each one ends up in the client that uses it,
/// whether that is a web page or an app binary.
///
/// There is one id per client, because Google issues a separate OAuth client per platform, and a token
/// carries the id of whichever client obtained it. All of them are checked against the same Google
/// project, so adding a platform is adding an OAuth client rather than a project.
/// </summary>
public sealed class GoogleAuthSettings
{
    public const string SectionName = "GoogleAuth";

    /// <summary>
    /// The web client's id. Unlike the mobile ones this is also handed to the browser (see
    /// ClientFlagsDto), which needs it to start the sign-in flow at all; empty hides the Google button
    /// rather than offering one that could only ever fail.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>The Orbit.Maui iOS app's own OAuth client id. Empty until that app is set up in Google Cloud Console.</summary>
    public string IosClientId { get; set; } = string.Empty;

    /// <summary>The Orbit.Maui Android app's own OAuth client id. Empty until that app is set up in Google Cloud Console.</summary>
    public string AndroidClientId { get; set; } = string.Empty;

    /// <summary>
    /// Every client id whose tokens this deployment accepts, with the unconfigured ones dropped - the
    /// audience allowlist <see cref="GoogleIdentityVerifier"/> checks a token against.
    ///
    /// It stays an explicit list on purpose. The audience check is what stops a token minted for some
    /// other Google application from signing its holder in here, so the one thing this must never become
    /// is "accept any audience": an empty list refuses everything rather than allowing everything.
    /// </summary>
    public IReadOnlyList<string> AcceptedClientIds
        => new[] { ClientId, IosClientId, AndroidClientId }
            .Where(clientId => !string.IsNullOrWhiteSpace(clientId))
            .Select(clientId => clientId.Trim())
            .ToList();

    /// <summary>False when no client at all is configured; Google sign-in then behaves as if it didn't exist.</summary>
    public bool IsConfigured => AcceptedClientIds.Count > 0;
}
