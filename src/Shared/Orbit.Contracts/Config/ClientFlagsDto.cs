namespace Orbit.Contracts.Config;

/// <summary>
/// Server-environment-driven flags the Blazor client can't determine on its own. ExceptionDetailsAllowed
/// reflects IWebHostEnvironment.IsDevelopment() - the hard gate on top of each user's own
/// NotificationSettings.ShowExceptionDetails preference, so a Production deployment never exposes
/// exception details/stack traces regardless of what an individual account has set.
/// </summary>
public sealed record ClientFlagsDto(bool ExceptionDetailsAllowed);
