using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Orbit.Api;
using Orbit.Api.RateLimiting;
using Orbit.Api.Tests.TestDoubles;
using Xunit;

namespace Orbit.Api.Tests.Auth;

/// <summary>
/// Covers what a caller who keeps trying actually gets back. The policies here are the ones Program.cs
/// installs - <see cref="RateLimiterPolicies.AddOrbitPolicies"/> is called by both, so this cannot pass
/// against a copy that has drifted from what ships.
///
/// What is deliberately not the real application is everything else: the endpoint under the policy is a
/// stand-in, because the question is whether the limiter refuses at the right point and against the
/// right caller, not whether signing in works.
/// </summary>
public sealed class AuthRateLimiterTests
{
    /// <summary>The window's budget, restated here so a change to it fails this test rather than passing quietly.</summary>
    private const int AuthRequestsPerWindow = 5;

    /// <summary>The same, for the bucket every anonymous caller shares - see RateLimitCeiling.</summary>
    private const int AnonymousAuthCeiling = 120;

    [Fact]
    public async Task The_sixth_attempt_in_a_window_is_refused_rather_than_queued()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        for (var attempt = 1; attempt <= AuthRequestsPerWindow; attempt++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/probe")).StatusCode);
        }

        // 429 rather than a delayed 200: the policy sets QueueLimit to 0 precisely so a caller working
        // through a password list is told no immediately instead of being handed a slower channel.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/probe")).StatusCode);
    }

    [Fact]
    public async Task One_caller_running_out_of_attempts_does_not_lock_anybody_else_out()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        // Signed-in callers are partitioned by user id, which is the whole point of that choice: behind
        // an ingress proxy every request carries the proxy's address, so an IP partition would be one
        // shared bucket and a stranger could lock a user out of their own account.
        for (var attempt = 1; attempt <= AuthRequestsPerWindow + 1; attempt++)
        {
            await client.GetAsync($"/probe?user=first");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/probe?user=first")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/probe?user=second")).StatusCode);
    }

    [Fact]
    public async Task Public_share_links_get_a_budget_of_their_own()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        // Far more generous than the auth budget, and separate from it - opening a handful of shared
        // links must not eat into somebody's ability to sign in.
        for (var attempt = 1; attempt <= AuthRequestsPerWindow + 1; attempt++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/public-probe")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/probe")).StatusCode);
    }

    /// <summary>
    /// The bug this fixes, and it was live: behind the Container Apps ingress every request carried the
    /// ingress's own address, so anonymous callers all landed in one partition and about five requests a
    /// minute from anywhere answered 429 to everybody trying to sign in. With the caller's real address
    /// reaching the limiter, one person running out of attempts is one person.
    /// </summary>
    [Fact]
    public async Task Two_anonymous_callers_from_different_addresses_do_not_share_a_budget()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        for (var attempt = 1; attempt <= AuthRequestsPerWindow + 1; attempt++)
        {
            await client.GetAsync("/probe?from=203.0.113.7");
        }

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            (await client.GetAsync("/probe?from=203.0.113.7")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/probe?from=198.51.100.4")).StatusCode);
    }

    /// <summary>
    /// The other half, and why the address alone is not enough to rest on. If a forwarded header can be
    /// forged, every request arrives in a partition of its own and the per-caller budget bounds nothing
    /// - so the anonymous callers share a ceiling as well as having their own budgets. It is generous
    /// on purpose: it is what an attacker can spend to make others wait, so it must not be easy to meet.
    /// </summary>
    [Fact]
    public async Task A_caller_inventing_a_new_address_each_time_still_meets_a_ceiling()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        var refusedAt = 0;
        for (var attempt = 1; attempt <= AnonymousAuthCeiling + 1 && refusedAt == 0; attempt++)
        {
            // A different address every time, so the per-caller budget is never touched.
            var response = await client.GetAsync($"/probe?from=203.0.113.{attempt % 256}");
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                refusedAt = attempt;
            }
        }

        Assert.Equal(AnonymousAuthCeiling + 1, refusedAt);
    }

    /// <summary>
    /// The reason this whole mechanism exists. Two hosts are two API instances, and a budget of five a
    /// minute has to mean five between them - not five each. With an in-memory window per process it
    /// meant five each, so the number in RateLimiterPolicies was really that number times however many
    /// replicas happened to be running.
    /// </summary>
    [Fact]
    public async Task Two_instances_share_one_budget_rather_than_getting_one_each()
    {
        var shared = new InMemoryRateLimitWindows();
        using var first = await StartHostAsync(shared);
        using var second = await StartHostAsync(shared);

        // Spent alternately, so neither instance's own window is what refuses: each sees only three
        // attempts of its own, which is well inside the five it would have allowed on its own.
        var clients = new[] { first.GetTestClient(), second.GetTestClient() };
        for (var attempt = 0; attempt < AuthRequestsPerWindow; attempt++)
        {
            Assert.Equal(
                HttpStatusCode.OK,
                (await clients[attempt % 2].GetAsync("/probe?user=shared")).StatusCode);
        }

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            (await clients[0].GetAsync("/probe?user=shared")).StatusCode);
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            (await clients[1].GetAsync("/probe?user=shared")).StatusCode);
    }

    /// <summary>
    /// A database that cannot be reached must not lock everybody out. The shared window is the second of
    /// two gates and the first one is the limiter this replica has always had, so losing the shared
    /// count falls back to the old behaviour rather than below it - or, just as importantly, rather than
    /// answering 429 to every sign-in because of a database hiccup.
    /// </summary>
    [Fact]
    public async Task An_unreachable_shared_window_falls_back_to_this_instance_rather_than_refusing()
    {
        var unreachable = new InMemoryRateLimitWindows { PretendTheDatabaseIsUnreachable = true };
        using var host = await StartHostAsync(unreachable);
        var client = host.GetTestClient();

        for (var attempt = 1; attempt <= AuthRequestsPerWindow; attempt++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/probe")).StatusCode);
        }

        // Still refused at this instance's own budget - the fallback is the old limiter, not no limiter.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/probe")).StatusCode);
    }

    /// <summary>
    /// Health probes must never be refused. Container Apps decides whether a revision is alive by
    /// probing them, so a 429 under load would have the platform restart the container - turning a busy
    /// minute into an outage, which is the opposite of what a flood stop is for.
    /// </summary>
    [Fact]
    public async Task A_flood_never_refuses_a_health_probe()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        // Comfortably past the per-caller flood limit, on one address, so anything not exempt is refused.
        for (var attempt = 1; attempt <= 620; attempt++)
        {
            await client.GetAsync("/probe?from=203.0.113.9");
        }

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            (await client.GetAsync("/probe?from=203.0.113.9")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/health?from=203.0.113.9")).StatusCode);
    }

    /// <summary>
    /// An endpoint with no policy of its own is still not unlimited - 130 of the 147 have none, and the
    /// global limiter is the only thing standing in front of them.
    /// </summary>
    [Fact]
    public async Task An_endpoint_with_no_policy_of_its_own_is_still_bounded()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        var refused = false;
        for (var attempt = 1; attempt <= 601 && !refused; attempt++)
        {
            var response = await client.GetAsync("/unlimited?from=198.51.100.9");
            refused = response.StatusCode == HttpStatusCode.TooManyRequests;
        }

        Assert.True(refused, "An endpoint outside every named policy was never refused.");
    }

    /// <summary>
    /// A host carrying nothing but the real policies and two endpoints to spend them on. Each gets a
    /// window store of its own unless one is handed in, which is what lets a test stage two instances. The "user"
    /// query parameter stands in for a bearer token: the policy partitions on the "sub" claim, and
    /// putting one there directly keeps the test about the limiter rather than about token validation.
    /// </summary>
    private static async Task<IHost> StartHostAsync(IRateLimitWindows? sharedWindows = null)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();

                    // Singleton, as in Program.cs - the policies capture it when a partition is first
                    // opened and keep it, so a scoped one would be used out of a disposed scope.
                    services.AddSingleton(sharedWindows ?? new InMemoryRateLimitWindows());
                    services.AddRateLimiter(options => options.AddOrbitPolicies());
                })
                .Configure(app =>
                {
                    app.Use(async (context, next) =>
                    {
                        if (context.Request.Query["user"] is [{ } userId, ..])
                        {
                            context.User = new ClaimsPrincipal(
                                new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId)], "Test"));
                        }

                        // Stands in for what UseForwardedHeaders does in Program.cs: by the time the
                        // limiter runs, RemoteIpAddress is the caller's rather than the proxy's.
                        if (context.Request.Query["from"] is [{ } address, ..])
                        {
                            context.Connection.RemoteIpAddress = IPAddress.Parse(address);
                        }

                        await next();
                    });

                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/probe", () => Results.Ok())
                            .RequireRateLimiting(RateLimiterPolicyNames.Auth);
                        endpoints.MapGet("/public-probe", () => Results.Ok())
                            .RequireRateLimiting(RateLimiterPolicyNames.PublicShare);

                        // No RequireRateLimiting: what 130 of Orbit's endpoints look like, and what
                        // the global limiter is the only thing in front of.
                        endpoints.MapGet("/unlimited", () => Results.Ok());
                        endpoints.MapGet("/health", () => Results.Ok());
                    });
                }))
            .StartAsync();

        return host;
    }
}
