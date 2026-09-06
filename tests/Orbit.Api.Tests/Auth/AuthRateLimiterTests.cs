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
                    });
                }))
            .StartAsync();

        return host;
    }
}
