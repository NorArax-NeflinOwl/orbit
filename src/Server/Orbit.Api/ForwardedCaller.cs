using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Orbit.Api;

/// <summary>
/// How the application decides who is calling, given that every request reaches it through a proxy.
///
/// The connection's own address is never the caller's: it is the Container Apps ingress in front of this
/// container. The caller is in X-Forwarded-For, and the whole question is which entry of that header to
/// believe - a client can put anything it likes at the left-hand end, and only the entries appended by
/// something we run can be trusted. Measured on the deployment, not reasoned about, the chains are:
///
///   phone, straight to this app     [forged?], caller                           ← ingress appends caller
///   browser, through nginx          [forged?], caller, caller, nginx-pod         ← nginx appends its view of
///                                                                                  the caller; the internal
///                                                                                  ingress appends nginx
///
/// The middleware walks the header from the right, one hop at a time, and takes another hop only while
/// the address it currently holds belongs to a known proxy. With <see cref="EnvironmentNetwork"/> as the
/// only known range, that is exactly right for both chains: the phone's stops after one hop at the
/// caller, because the caller is not in our range; the browser's takes a second hop past the nginx pod,
/// because that is, and stops at the caller for the same reason. So <c>ForwardLimit</c> is 2, and the
/// second hop cannot be bought by anybody outside the environment - it is only taken when the
/// ingress-appended, unforgeable address of the previous hop is one of ours.
///
/// What this cannot survive is nginx reaching this app by its public name. That sends the request out of
/// the environment and back in, the public ingress appends the environment's egress NAT address instead
/// of the nginx pod, and the walk stops there - every browser user then shares one address. It shipped
/// that way once (the request log said <c>from 20.215.81.0</c> for everybody) and is the reason
/// nginx.azure.conf names the internal FQDN. ForwardedCallerTests keeps both chains and that failure
/// pinned.
///
/// Extracted from Program.cs so that a test can run the very configuration that ships, the way
/// RateLimiterPolicies.AddOrbitPolicies is shared with its test.
/// </summary>
public static class ForwardedCaller
{
    /// <summary>
    /// The Container Apps environment's own range - measured as the ingress address in orbit-web's
    /// access log (100.100.0.124), and the peer this app sees for both of its ingresses. Everything in it
    /// is something we run; nothing outside it is trusted to speak for a caller.
    /// </summary>
    public static readonly System.Net.IPNetwork EnvironmentNetwork = new(IPAddress.Parse("100.100.0.0"), 16);

    public static ForwardedHeadersOptions Options()
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            // Two, not one: see the browser chain above. Not more, because there is no third proxy we run.
            ForwardLimit = 2
        };

        // The defaults trust loopback only, which would leave every forwarded header unread here. Cleared
        // and replaced rather than added to, so the list is exactly what the comment says it is.
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Add(EnvironmentNetwork);
        return options;
    }
}
