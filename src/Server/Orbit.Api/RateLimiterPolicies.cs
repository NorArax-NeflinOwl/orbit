using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Orbit.Api;

/// <summary>
/// The rate-limiting policies named by <see cref="RateLimiterPolicyNames"/>, in one place Program.cs
/// calls and a test can call too. They used to be written inline where they are applied, which left the
/// exact thing they exist to do - answer 429 rather than let a caller keep guessing - reachable only by
/// running the whole application.
/// </summary>
public static class RateLimiterPolicies
{
    public static void AddOrbitPolicies(this RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        // Brute-force protection for /api/auth/register and /api/auth/login (see AuthEndpoints for why
        // /refresh and /logout don't use this policy) and for the signed-in endpoints that change an
        // account: 5 requests per minute per caller, with no queueing, so a caller that exceeds this
        // gets an immediate 429 instead of waiting.
        //
        // Partitioned by user id whenever the caller is signed in, and only by IP address when there is
        // nobody to name. Behind an ingress proxy - which is how this runs in Azure Container Apps -
        // RemoteIpAddress is the proxy's own address, identical for every visitor, so an IP partition
        // there is really one shared bucket: five email-verification codes a minute for the whole
        // installation, and a signed-in user locked out by strangers. The user id is both the honest
        // key for those endpoints and one no forwarded header has to be trusted for.
        options.AddPolicy(RateLimiterPolicyNames.Auth, httpContext => RateLimitPartition.GetFixedWindowLimiter(
            // "sub", not ClaimTypes.NameIdentifier: MapInboundClaims is off above, so the token's own
            // claim names survive unmapped - which is what every endpoint here reads too.
            partitionKey: httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

        // Public share links: the token in the URL is the whole access check, so this is the one
        // endpoint where guessing is worth attempting at all. 30 a minute per IP is far more than
        // opening links by hand needs and far less than working through a keyspace requires - the
        // token's own length is what makes that hopeless; this just removes the free attempts.
        options.AddPolicy(RateLimiterPolicyNames.PublicShare, httpContext => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    }
}
