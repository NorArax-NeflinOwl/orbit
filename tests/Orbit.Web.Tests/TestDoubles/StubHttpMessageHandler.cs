namespace Orbit.Web.Tests.TestDoubles;

/// <summary>
/// Routes each outgoing request to a caller-supplied delegate instead of the network, so HTTP-dependent
/// code can be exercised deterministically in a unit test.
/// </summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(respond(request));
}
