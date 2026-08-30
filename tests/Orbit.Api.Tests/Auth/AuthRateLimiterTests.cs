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
    /// A host carrying nothing but the real policies and two endpoints to spend them on. The "user"
    /// query parameter stands in for a bearer token: the policy partitions on the "sub" claim, and
    /// putting one there directly keeps the test about the limiter rather than about token validation.
    /// </summary>
    private static async Task<IHost> StartHostAsync()
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddRateLimiter(options => options.AddOrbitPolicies());
                })
                .Configure(app =>
                {
                    app.Use(async (context, next) =>
                    {
                        if (context.Request.Query["user"] is [var userId, ..])
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
