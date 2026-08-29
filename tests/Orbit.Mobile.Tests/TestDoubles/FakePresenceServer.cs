using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Users;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>The two presence endpoints, and what the last caller told them.</summary>
internal sealed class FakePresenceServer : HttpMessageHandler
{
    public bool IsUnreachable { get; set; }

    /// <summary>
    /// Refuses to record an availability while leaving the heartbeats alone - a request that arrived
    /// and was turned down, as opposed to one that never got there.
    /// </summary>
    public bool RefusesAvailability { get; set; }

    /// <summary>What this account last said it wanted to be, or null if it never said.</summary>
    public string? Availability { get; private set; }

    public int HeartbeatCount { get; private set; }

    /// <summary>Every request that reached here, refused ones included.</summary>
    public int RequestCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;

        if (IsUnreachable)
        {
            throw new HttpRequestException("No such host is known.");
        }

        if (request.RequestUri!.AbsolutePath.EndsWith("/heartbeat", StringComparison.Ordinal))
        {
            HeartbeatCount++;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (RefusesAvailability)
        {
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }

        Availability = (await request.Content!.ReadFromJsonAsync<SetAvailabilityRequest>(cancellationToken))!.Availability;
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.example/") };
}
