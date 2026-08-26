using System.Net;
using System.Net.Http.Headers;

namespace Orbit.Mobile.Authentication;

/// <summary>
/// Attaches the access token to every API call and, when one comes back 401, refreshes once and sends
/// it again. Access tokens are short-lived by design (fifteen minutes), so an app left open across that
/// boundary would otherwise show the user a failure for something that only needed a new token.
///
/// Retrying at most once is the point: a second 401 after a successful refresh means the request is
/// genuinely unauthorised, and retrying further would loop.
/// </summary>
public sealed class AuthorizationMessageHandler : DelegatingHandler
{
    private readonly SessionStore _sessionStore;
    private readonly TokenRefreshService _tokenRefreshService;

    public AuthorizationMessageHandler(SessionStore sessionStore, TokenRefreshService tokenRefreshService)
    {
        _sessionStore = sessionStore;
        _tokenRefreshService = tokenRefreshService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Buffered before the first send so the retry below has a body to send too - a request that has
        // already gone out cannot be reused, and a stream-backed one cannot be read twice.
        var body = await BufferBodyAsync(request, cancellationToken);

        await AttachAccessTokenAsync(request);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        if (!await _tokenRefreshService.TryRefreshAsync(cancellationToken))
        {
            return response;
        }

        response.Dispose();
        using var retry = CloneWithBody(request, body);
        await AttachAccessTokenAsync(retry);
        return await base.SendAsync(retry, cancellationToken);
    }

    private async Task AttachAccessTokenAsync(HttpRequestMessage request)
    {
        if (await _sessionStore.GetAsync() is { } session)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        }
    }

    private static async Task<byte[]?> BufferBodyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);

    private static HttpRequestMessage CloneWithBody(HttpRequestMessage original, byte[]? body)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri) { Version = original.Version };

        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (body is null)
        {
            return clone;
        }

        clone.Content = new ByteArrayContent(body);
        foreach (var header in original.Content!.Headers)
        {
            clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
