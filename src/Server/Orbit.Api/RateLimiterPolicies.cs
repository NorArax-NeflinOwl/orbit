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
    /// Sized to measured traffic, not to caution. Thirty days of the deployment's request log
    /// (2026-08-08 to 2026-09-07; 2,464 minutes carried any API traffic at all): anonymous sign-ins,
    /// registrations and password resets peaked at **4 in one minute** and totalled 34 for the month;
    /// public share links were opened **0 times** outside one deliberate probe.
    ///
    /// So 30 is six callers each spending their whole per-caller budget in the same minute, and seven
    /// times the busiest minute ever seen; 150 is five callers' worth of public share reads. They were
    /// 120 and 600 while the forwarded address was still possibly forgeable - the case they were built
    /// for - and that case is now measured not to apply (ForwardedCallerTests), which leaves them one
    /// job: bounding a distributed guess, many addresses each under its own 5 a minute. The smaller the
    /// ceiling, the tighter that bound - and the closer to the day honest traffic outgrows it and 429s
    /// everybody at once. Grow these with the user count, from the measurement below, not on a hunch:
    ///
    ///   traces | where message startswith 'HTTP POST /api/auth/'
    ///          | where message has 'login' or message has 'register' or message has 'password-reset'
    ///          | summarize n=count() by bin(timestamp, 1m) | summarize max(n), percentile(n, 99)
    /// </summary>
    private const int AnonymousAuthCeiling = 30;

    private const int PublicShareCeiling = 150;

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
    /// Both sized from thirty days of the request log. An open browser costs under two requests a second
    /// across every endpoint it touches; the busiest single caller in any minute was 123. Across every
    /// caller together, the median minute is 65 requests, the 99th percentile 205, and the busiest
    /// ordinary minute 287 - so the overall floor is ten times that. (One minute in the month reached
    /// 4,332, from one caller running the same four chat calls sixteen times a second; that is the
    /// per-caller limit's job, and it is recorded in info/future-plan.md.) Grow the overall number with
    /// the user count: it is a floor under many callers, and 65 a minute is what one or two cost.
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

    private const int FloodStopOverall = 3000;

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
