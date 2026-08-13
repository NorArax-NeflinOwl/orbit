namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// Routes each outgoing request to a caller-supplied delegate instead of the network, so HTTP-dependent
/// code can be exercised deterministically in a unit test.
/// </summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => respond(request, cancellationToken);
}

/// <summary>Hands out <see cref="HttpClient"/> instances backed by a single stub handler, regardless of the requested client name.</summary>
internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
