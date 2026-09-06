using System.Net;
using Orbit.Api.Telemetry;
using Xunit;

namespace Orbit.Api.Tests.Telemetry;

/// <summary>
/// What a request's log line is allowed to say about who made it.
///
/// Two things have to hold at once, and they pull against each other: the value must vary between
/// callers, or it cannot answer the question it exists for - whether UseForwardedHeaders is resolving
/// anything at all - and it must not identify a person, because a log line outlives the request by
/// weeks and this deployment ships its logs to Application Insights.
/// </summary>
public sealed class ClientNetworkTests
{
    [Fact]
    public void An_address_keeps_its_network_and_loses_the_host()
    {
        Assert.Equal("46.205.200.0", ClientNetwork.Of(IPAddress.Parse("46.205.200.84")));
        Assert.Equal("100.100.0.0", ClientNetwork.Of(IPAddress.Parse("100.100.0.124")));
    }

    /// <summary>
    /// The whole point: the ingress and a real caller have to look different, or the log answers
    /// nothing. Two callers on different networks have to look different too.
    /// </summary>
    [Fact]
    public void Different_networks_stay_distinguishable()
    {
        var ingress = ClientNetwork.Of(IPAddress.Parse("100.100.0.124"));
        var caller = ClientNetwork.Of(IPAddress.Parse("46.205.200.84"));
        var another = ClientNetwork.Of(IPAddress.Parse("198.51.100.7"));

        Assert.NotEqual(ingress, caller);
        Assert.NotEqual(caller, another);
    }

    /// <summary>
    /// And the point in the other direction: two people behind one network must not be told apart by
    /// this, which is what makes it safe to keep.
    /// </summary>
    [Fact]
    public void Two_callers_on_one_network_are_not_told_apart()
    {
        Assert.Equal(
            ClientNetwork.Of(IPAddress.Parse("46.205.200.84")),
            ClientNetwork.Of(IPAddress.Parse("46.205.200.201")));
    }

    [Fact]
    public void IPv6_keeps_only_the_routing_prefix()
    {
        // /48 - the site, not the interface, and not the device the interface identifier would name.
        Assert.Equal("2001:db8:1234::", ClientNetwork.Of(IPAddress.Parse("2001:db8:1234:5678:9abc:def0:1234:5678")));
        Assert.NotEqual(
            ClientNetwork.Of(IPAddress.Parse("2001:db8:1234::1")),
            ClientNetwork.Of(IPAddress.Parse("2001:db8:9999::1")));
    }

    /// <summary>
    /// Kestrel reports an IPv4 caller as ::ffff:a.b.c.d when listening dual-stack, and that is exactly
    /// the case this diagnostic has to stay readable in - the one where forwarded headers resolved
    /// nothing and the peer is all there is. Masked as an IPv6 address it collapses to "::", which
    /// would answer the question with silence. Found by running it rather than by reading it.
    /// </summary>
    [Fact]
    public void An_IPv4_caller_reported_as_IPv6_is_still_an_IPv4_network()
    {
        Assert.Equal("46.205.200.0", ClientNetwork.Of(IPAddress.Parse("::ffff:46.205.200.84")));
        Assert.Equal("100.100.0.0", ClientNetwork.Of(IPAddress.Parse("::ffff:100.100.0.124")));
    }

    [Fact]
    public void Nothing_to_say_says_nothing()
    {
        Assert.Equal(ClientNetwork.Unknown, ClientNetwork.Of(null));
    }
}
