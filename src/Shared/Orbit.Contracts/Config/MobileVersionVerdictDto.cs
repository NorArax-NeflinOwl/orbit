namespace Orbit.Contracts.Config;

/// <summary>
/// What the server tells a mobile app to do about its own version. <paramref name="Verdict"/> is one of
/// "Supported", "UpdateAvailable", or "UpdateRequired" (see Orbit.Core's MobileVersionVerdict).
///
/// The app caches this against the version it asked about, so it can still decide while offline. It
/// blocks only on a verdict it actually holds - never because the server was unreachable, which would
/// break the app in exactly the situation offline support exists for.
/// </summary>
/// <param name="LatestVersion">The newest released version, or null when this deployment hasn't configured one.</param>
/// <param name="UpdateUrl">Where to send the user to update - a store listing. Null when unconfigured.</param>
public sealed record MobileVersionVerdictDto(string Verdict, string? LatestVersion, string? UpdateUrl);
