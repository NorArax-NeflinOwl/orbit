using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// Stands in for the network so a test can say what the server did - answered, refused, or was never
/// there at all - without one being reachable.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;

    private StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        => _respond = respond;

    /// <summary>
    /// Every request that reached it. Recorded as a snapshot rather than as the live
    /// <see cref="HttpRequestMessage"/>, because a caller that correctly disposes the request it built
    /// would leave a test holding disposed content.
    /// </summary>
    public List<RecordedRequest> ReceivedRequests { get; } = [];

    public static StubHttpMessageHandler RespondingWith(object payload)
        => new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(payload)
        }));

    public static StubHttpMessageHandler RespondingWith(HttpStatusCode statusCode)
        => new((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)));

    /// <summary>An unreachable server - no DNS, no route, aeroplane mode.</summary>
    public static StubHttpMessageHandler Unreachable()
        => new((_, _) => throw new HttpRequestException("No such host is known."));

    /// <summary>A server that answers, but not before the caller has given up waiting.</summary>
    public static StubHttpMessageHandler NeverAnswering()
        => new(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new UnreachableException();
        });

    public static StubHttpMessageHandler Custom(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        => new(respond);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ReceivedRequests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri,
            request.Headers.Authorization?.ToString(),
            request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
            request.Content?.Headers.ContentType?.MediaType));

        return await _respond(request, cancellationToken);
    }

    public HttpClient ToHttpClient() => new(this) { BaseAddress = new Uri("https://orbit.example/") };
}

/// <summary>What a request looked like when it arrived, kept past the request's own lifetime.</summary>
internal sealed record RecordedRequest(
    HttpMethod Method, Uri? Uri, string? Authorization, string? Body, string? ContentType);
