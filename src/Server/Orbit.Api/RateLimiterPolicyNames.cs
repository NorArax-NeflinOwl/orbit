namespace Orbit.Api;

/// <summary>
/// Names of the rate-limiting policies configured in Program.cs, referenced by the endpoint groups that
/// apply them via RequireRateLimiting - kept as constants so the two call sites can't drift apart with a
/// typo.
/// </summary>
public static class RateLimiterPolicyNames
{
    public const string Auth = "Auth";
}
