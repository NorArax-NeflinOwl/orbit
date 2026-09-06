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
        options.GlobalLimiter = FloodStop();
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
        options.AddPolicy(RateLimiterPolicyNames.Auth, httpContext =>
        {
            // "sub", not ClaimTypes.NameIdentifier: MapInboundClaims is off above, so the token's own
            // claim names survive unmapped - which is what every endpoint here reads too.
            var signedInAs = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            return Partition(
                httpContext,
                RateLimiterPolicyNames.Auth,
                signedInAs ?? Caller(httpContext),
                permitLimit: 5,
                // A signed-in caller is named by a token the server issued, so their partition cannot be
                // forged and a ceiling over them would only let strangers spend a budget they cannot
                // reach. Anonymous callers - signing in, registering, asking for a password reset - are
                // named by an address, so theirs gets one. See RateLimitCeiling.
                ceiling: signedInAs is null
                    ? new RateLimitCeiling($"{RateLimiterPolicyNames.Auth}:anonymous", AnonymousAuthCeiling)
                    : null);
        });

        // Public share links: the token in the URL is the whole access check, so this is the one
        // endpoint where guessing is worth attempting at all. 30 a minute per IP is far more than
        // opening links by hand needs and far less than working through a keyspace requires - the
        // token's own length is what makes that hopeless; this just removes the free attempts.
        options.AddPolicy(RateLimiterPolicyNames.PublicShare, httpContext => Partition(
            httpContext,
            RateLimiterPolicyNames.PublicShare,
            Caller(httpContext),
            permitLimit: 30,
            // Nobody is signed in on this path by definition, so the address is all there ever is.
            ceiling: new RateLimitCeiling(
                $"{RateLimiterPolicyNames.PublicShare}:all", PublicShareCeiling)));
    }

    /// <summary>
    /// 24 and 20 times the per-caller budgets. Deliberately far above honest traffic: the access log of
    /// the deployment shows an open browser costing under two requests a second across every endpoint,
    /// and anonymous sign-ins are a handful a minute. What these bound is the case where the forwarded
    /// address can be forged and every request lands in a partition of its own - 120 password attempts a
    /// minute rather than no limit at all. They cannot be tightened much further without becoming a
    /// denial of service in their own right, which is the trade RateLimitCeiling describes.
    /// </summary>
    private const int AnonymousAuthCeiling = 120;

    private const int PublicShareCeiling = 600;

    /// <summary>
    /// Who to count this against when nobody is signed in.
    ///
    /// Behind the Container Apps ingress this used to be the ingress's own address for every visitor -
    /// measured in the access log, not assumed - which made one shared bucket of the whole policy: about
    /// five requests a minute from anywhere answered 429 to everybody trying to sign in. nginx now
    /// derives the caller from the forwarded chain and Program.cs reads it (UseForwardedHeaders), so
    /// this is a real address where the chain carries one. Where it does not, it falls back to exactly
    /// what it was before, and the ceiling above is what keeps that from being the only defence.
    /// </summary>
    private static string Caller(HttpContext httpContext)
        => httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>
    /// A coarse limit on everything, so the endpoints with no policy of their own - 130 of the 147 -
    /// are not simply unlimited.
    ///
    /// **In memory, unlike the named policies, and that is not an oversight.** Those consult a window
    /// shared through PostgreSQL, which costs a round trip per permitted request; that is right for the
    /// handful of endpoints where the exact number matters and quite wrong for every request in Orbit.
    /// What this has to do is blunt a flood, and a flood is orders of magnitude away from the limit, so
    /// counting per instance is close enough. With more than one replica the effective limit is that
    /// many times the number below, which for a flood stop changes nothing worth having.
    ///
    /// Two chained partitions. The first is per caller and is the one that does the work. The second is
    /// keyed on nothing and is the floor under it, for the same reason RateLimitCeiling exists: where a
    /// forwarded address can be forged, per-caller buckets stop bounding anything. It matters more here
    /// than at the edge, because the phone talks to this application directly and nginx's own limits
    /// never see that traffic.
    ///
    /// Both are far above real use. The deployment's access log shows an open browser costing under two
    /// requests a second across every endpoint it touches.
    /// </summary>
    private static PartitionedRateLimiter<HttpContext> FloodStop()
        => PartitionedRateLimiter.CreateChained(
            PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                Exempt(httpContext)
                    ? RateLimitPartition.GetNoLimiter("exempt")
                    : RateLimitPartition.GetFixedWindowLimiter(
                        $"flood:{Caller(httpContext)}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = FloodStopPerCaller,
                            Window = Window,
                            QueueLimit = 0
                        })),
            PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                Exempt(httpContext)
                    ? RateLimitPartition.GetNoLimiter("exempt")
                    : RateLimitPartition.GetFixedWindowLimiter(
                        "flood:all",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = FloodStopOverall,
                            Window = Window,
                            QueueLimit = 0
                        })));

    /// <summary>
    /// The health endpoints, and nothing else. Container Apps decides whether this revision is alive by
    /// probing them, so answering one with 429 under load would have the platform restart the container
    /// - turning a busy minute into an outage, which is the exact opposite of what a flood stop is for.
    /// </summary>
    private static bool Exempt(HttpContext httpContext)
        => httpContext.Request.Path.StartsWithSegments("/health");

    private const int FloodStopPerCaller = 600;

    private const int FloodStopOverall = 6000;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The policy name is part of the key rather than only part of the lookup, because the shared window
    /// is one table for both policies: without it, opening shared links would spend somebody's budget
    /// for signing in, which is exactly what the two separate budgets exist to prevent.
    /// </summary>
    private static RateLimitPartition<string> Partition(
        HttpContext httpContext, string policy, string caller, int permitLimit,
        RateLimitCeiling? ceiling = null)
    {
        var partition = $"{policy}:{caller}";

        // Resolved from the request that first opened this partition, and then kept for as long as the
        // partition is cached - which is only correct because IRateLimitWindows is a singleton. A scoped
        // registration here would be captured out of a disposed scope and fail on the second caller.
        return RateLimitPartition.Get(partition, _ => new SharedFixedWindowRateLimiter(
            partition,
            permitLimit,
            Window,
            httpContext.RequestServices.GetRequiredService<IRateLimitWindows>(),
            ceiling));
    }
}
