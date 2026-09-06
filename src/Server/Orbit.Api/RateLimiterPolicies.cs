using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using Orbit.Api.RateLimiting;

namespace Orbit.Api;

/// <summary>
/// The rate-limiting policies named by <see cref="RateLimiterPolicyNames"/>, in one place Program.cs
/// calls and a test can call too. They used to be written inline where they are applied, which left the
/// exact thing they exist to do - answer 429 rather than let a caller keep guessing - reachable only by
/// running the whole application.
///
/// Every budget here is spent from a window shared by all API instances - see
/// <see cref="SharedFixedWindowRateLimiter"/>. In-memory windows would mean each replica granting the
/// whole budget, so the number written below would quietly become that number times however many
/// replicas happen to be running: a limit set by a scaling decision rather than by this file.
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
        options.AddPolicy(RateLimiterPolicyNames.Auth, httpContext => Partition(
            httpContext,
            RateLimiterPolicyNames.Auth,
            // "sub", not ClaimTypes.NameIdentifier: MapInboundClaims is off above, so the token's own
            // claim names survive unmapped - which is what every endpoint here reads too.
            httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            permitLimit: 5));

        // Public share links: the token in the URL is the whole access check, so this is the one
        // endpoint where guessing is worth attempting at all. 30 a minute per IP is far more than
        // opening links by hand needs and far less than working through a keyspace requires - the
        // token's own length is what makes that hopeless; this just removes the free attempts.
        options.AddPolicy(RateLimiterPolicyNames.PublicShare, httpContext => Partition(
            httpContext,
            RateLimiterPolicyNames.PublicShare,
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            permitLimit: 30));
    }

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The policy name is part of the key rather than only part of the lookup, because the shared window
    /// is one table for both policies: without it, opening shared links would spend somebody's budget
    /// for signing in, which is exactly what the two separate budgets exist to prevent.
    /// </summary>
    private static RateLimitPartition<string> Partition(
        HttpContext httpContext, string policy, string caller, int permitLimit)
    {
        var partition = $"{policy}:{caller}";

        // Resolved from the request that first opened this partition, and then kept for as long as the
        // partition is cached - which is only correct because IRateLimitWindows is a singleton. A scoped
        // registration here would be captured out of a disposed scope and fail on the second caller.
        return RateLimitPartition.Get(partition, _ => new SharedFixedWindowRateLimiter(
            partition,
            permitLimit,
            Window,
            httpContext.RequestServices.GetRequiredService<IRateLimitWindows>()));
    }
}
