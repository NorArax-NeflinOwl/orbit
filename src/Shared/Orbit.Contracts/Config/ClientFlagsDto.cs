namespace Orbit.Contracts.Config;

/// <summary>
/// Server-environment-driven flags the Blazor client can't determine on its own. ExceptionDetailsAllowed
/// reflects IWebHostEnvironment.IsDevelopment() - the hard gate on top of each user's own
/// NotificationSettings.ShowExceptionDetails preference, so a Production deployment never exposes
/// exception details/stack traces regardless of what an individual account has set.
///
/// GoogleClientId is empty unless this deployment has Google sign-in configured; the client hides the
/// Google button entirely in that case, rather than offering a button that could only ever fail.
/// </summary>
/// <param name="WebAddress">
/// Where the browser client lives, so a client that is not the browser can build a link into it. A
/// public share link points at a page in the web app, and a phone has no way of knowing that address on
/// its own - the browser reads it off its own origin, and there is nothing equivalent to read on iOS.
/// Empty when the deployment has not said, in which case a client that needs it should not offer links
/// it cannot build.
/// </param>
/// <param name="GoogleAndroidClientId">
/// The Android app's own Google client id, empty when this deployment has not set one. Google issues a
/// separate OAuth client per platform and a token carries the id of whichever client obtained it, so the
/// phone cannot use the browser's - see GoogleAuthSettings. Served rather than built into the app for
/// the same reason <see cref="WebAddress"/> is: it belongs to the deployment, and an app binary that
/// carried it could only ever talk to the one deployment it was built for.
/// </param>
/// <param name="GoogleIosClientId">The same, for the iOS app.</param>
public sealed record ClientFlagsDto(
    bool ExceptionDetailsAllowed, string GoogleClientId, string WebAddress = "",
    string GoogleAndroidClientId = "", string GoogleIosClientId = "");
