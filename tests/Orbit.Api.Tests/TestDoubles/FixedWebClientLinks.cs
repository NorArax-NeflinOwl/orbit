using Orbit.Core.Notifications;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// A fixed answer for whatever this deployment's public address has been configured as - or none,
/// matching a fresh checkout where <c>WebClientBaseUrl</c> was never set.
/// </summary>
internal sealed class FixedWebClientLinks(string? baseUrl = null) : IWebClientLinks
{
    public string? For(string relativePath) => baseUrl is null ? null : $"{baseUrl}{relativePath}";
}
