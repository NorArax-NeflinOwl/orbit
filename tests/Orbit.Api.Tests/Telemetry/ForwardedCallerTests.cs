using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orbit.Api;
using Xunit;

namespace Orbit.Api.Tests.Telemetry;

/// <summary>
/// Which X-Forwarded-For entry the application believes, run against the real middleware with the real
/// configuration - <see cref="ForwardedCaller.Options"/> is what Program.cs installs, so this cannot pass
/// against a copy that has drifted.
///
/// Every chain here was measured on the deployment before it was written down. The one that fails is
/// kept deliberately: it is what shipped once, it is why nginx has to reach the API by its internal name,
/// and reasoning about the walk instead of running it is how it got through the first time.
/// </summary>
public sealed class ForwardedCallerTests
{
    private const string Ingress = "100.100.0.1";
    private const string NginxPod = "100.100.0.9";
    private const string Caller = "46.205.200.84";
    private const string Forged = "203.0.113.250";
    private const string EgressNat = "20.215.81.5";

    [Theory]
    // The phone, straight to the API: the ingress appends the caller and the walk stops there.
    [InlineData(Ingress, Caller, Caller)]
    // The same with a forged prefix: the forgery sits to the left of what the ingress appended and is
    // never reached.
    [InlineData(Ingress, Forged + ", " + Caller, Caller)]
    // The browser, through nginx inside the environment: nginx appends its view of the caller, the
    // internal ingress appends nginx. The pod is ours, so the walk takes a second hop past it.
    [InlineData(Ingress, Caller + ", " + Caller + ", " + NginxPod, Caller)]
    // Forged, through nginx: still the caller.
    [InlineData(Ingress, Forged + ", " + Caller + ", " + Caller + ", " + NginxPod, Caller)]
    // Should the internal ingress not append the pod after all, one hop is enough and two do no harm.
    [InlineData(Ingress, Caller + ", " + Caller, Caller)]
    public async Task The_caller_is_the_rightmost_address_that_is_not_one_of_ours(
        string peer, string forwardedFor, string expected)
    {
        Assert.Equal(expected, await WhoIsCallingAsync(peer, forwardedFor));
    }

    /// <summary>
    /// What shipped once. nginx reaching the API by its *public* name sends the request out of the
    /// environment and back in; the public ingress appends the egress NAT address, which is nobody we
    /// run, so the walk stops on it and every browser user shares one address. The request log read
    /// "from 20.215.81.0" for everybody. This test is why nginx.azure.conf names the internal FQDN.
    /// </summary>
    [Fact]
    public async Task Through_the_public_ingress_every_browser_user_looks_like_the_environment()
    {
        Assert.Equal(EgressNat, await WhoIsCallingAsync(Ingress, $"{Caller}, {Caller}, {EgressNat}"));
    }

    /// <summary>
    /// A header from a peer we do not run is not read at all. This is the property that lets the header
    /// be trusted in the first place: nothing outside the environment can name a caller.
    /// </summary>
    [Fact]
    public async Task A_header_from_an_unknown_peer_is_ignored()
    {
        Assert.Equal("198.51.100.7", await WhoIsCallingAsync("198.51.100.7", Caller));
    }

    /// <summary>
    /// The second hop is taken only past one of our own addresses - so a caller outside the environment
    /// cannot make the walk go two hops, however many entries it writes.
    /// </summary>
    [Fact]
    public async Task Two_hops_are_not_available_to_a_caller_outside_the_environment()
    {
        // The ingress appends the caller; the caller's own entries to the left are never consulted.
        Assert.Equal(Caller, await WhoIsCallingAsync(Ingress, $"{Forged}, {Forged}, {Caller}"));
    }

    /// <summary>
    /// A host with nothing but the real forwarded-header configuration and an endpoint that reports the
    /// address it ended up with. The peer is set by a header the test sends, standing in for the TCP
    /// connection the middleware would otherwise read it from; it runs before UseForwardedHeaders, as
    /// the connection would be established before it.
    /// </summary>
    private static async Task<string?> WhoIsCallingAsync(string peer, string forwardedFor)
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services => services.AddRouting())
                .Configure(app =>
                {
                    app.Use(async (context, next) =>
                    {
                        context.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers["X-Test-Peer"]!);
                        await next();
                    });

                    app.UseForwardedHeaders(ForwardedCaller.Options());
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapGet(
                        "/who", (HttpContext context) => Results.Text(context.Connection.RemoteIpAddress?.ToString() ?? "")));
                }))
            .StartAsync();

        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Peer", peer);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        return await client.GetStringAsync("/who");
    }
}
