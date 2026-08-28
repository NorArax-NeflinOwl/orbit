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
public sealed record ClientFlagsDto(bool ExceptionDetailsAllowed, string GoogleClientId, string WebAddress = "");
