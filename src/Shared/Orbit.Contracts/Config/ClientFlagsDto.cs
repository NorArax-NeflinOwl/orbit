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
public sealed record ClientFlagsDto(bool ExceptionDetailsAllowed, string GoogleClientId);
