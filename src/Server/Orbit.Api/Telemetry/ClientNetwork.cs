using System.Net;
using System.Net.Sockets;

namespace Orbit.Api.Telemetry;

/// <summary>
/// The network a request came from, with the part that identifies one person removed.
///
/// It exists to answer a question that turned out to be unanswerable from outside: whether
/// UseForwardedHeaders is actually resolving the caller, or silently doing nothing and leaving every
/// request looking like it came from the ingress. Both look identical in every other way - the
/// application works, the rate limiter refuses at the right count - and the difference is whether the
/// per-caller limits are per caller or one bucket for everybody. A configuration that is valid and
/// inert is exactly how the nginx side of this got shipped broken once already.
///
/// **Masked, because a whole address is personal data and a log line outlives the request by weeks.**
/// The last octet of an IPv4 address is dropped and everything below the first 48 bits of an IPv6 one,
/// which is enough to tell the ingress (100.100.x.x, always the same) from real callers (varied), and
/// enough to say "this network is making a lot of requests" without saying who. It is the same trade
/// Application Insights makes by default, and the same one the nginx access log makes when it drops the
/// query string from the live connection's path.
/// </summary>
public static class ClientNetwork
{
    public const string Unknown = "unknown";

    public static string Of(IPAddress? address)
    {
        if (address is null)
        {
            return Unknown;
        }

        // Kestrel reports an IPv4 caller as ::ffff:a.b.c.d when it is listening dual-stack, and that is
        // the very case this has to be readable in - the one where forwarded headers resolved nothing
        // and what is left is the peer. Masked as IPv6 it would collapse to "::" and say nothing at all.
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => Mask(address, keepBytes: 3),
            AddressFamily.InterNetworkV6 => Mask(address, keepBytes: 6),
            _ => Unknown
        };
    }

    private static string Mask(IPAddress address, int keepBytes)
    {
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, keepBytes, bytes.Length - keepBytes);
        return new IPAddress(bytes).ToString();
    }
}
