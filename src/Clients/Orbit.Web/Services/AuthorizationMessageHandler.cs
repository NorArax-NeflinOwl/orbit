using System.Net;
using System.Net.Http.Headers;

namespace Orbit.Web.Services;

/// <summary>
/// Attaches the signed-in user's access token to every outgoing request made through it. If the API
/// rejects a request with 401 (the access token expired), transparently exchanges the stored refresh
/// token for a new access token via <see cref="TokenRefreshService"/> and retries the original request
/// once.
/// </summary>
/// <remarks>
/// This type itself is registered Transient in Program.cs - it must be, since IHttpClientFactory
/// mutates a handler's InnerHandler while assembling each client's pipeline, and throws if the same
/// instance is reused across more than one. Everything it depends on
/// (TokenStore/TokenRefreshService/OrbitAuthenticationStateProvider) is Singleton though, which is what
/// actually matters here: AddHttpMessageHandler resolves this handler from IHttpClientFactory's own
/// internal, periodically-rotating DI scope, not the app's one real scope, so a Scoped dependency would
/// silently be a throwaway instance unrelated to what the rest of the app is using - which is exactly
/// what made <see cref="OrbitAuthenticationStateProvider.NotifyAuthenticationStateChanged"/> below a
/// no-op the first time this was wired up: it fired on an OrbitAuthenticationStateProvider instance
/// nothing else was subscribed to.
/// </remarks>
public sealed class AuthorizationMessageHandler(
    TokenStore tokenStore, TokenRefreshService tokenRefreshService, OrbitAuthenticationStateProvider authenticationStateProvider)
    : DelegatingHandler
{
    private readonly TokenStore _tokenStore = tokenStore;
    private readonly TokenRefreshService _tokenRefreshService = tokenRefreshService;
    private readonly OrbitAuthenticationStateProvider _authenticationStateProvider = authenticationStateProvider;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await AttachAccessTokenAsync(request);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        if (!await _tokenRefreshService.TryRefreshAsync(cancellationToken))
        {
            // The session is genuinely over (the refresh token is invalid or expired too). Every
            // caller that lands here already has its own "catch the 401, NavigateTo /login" handling
            // for its own page content, but none of them think to also invalidate the authentication
            // state - without this, MainLayout's sidebar (which listens for exactly this
            // AuthenticationStateChanged notification, not the token itself) and any [Authorize]-gated
            // route would keep rendering as signed-in on top of whatever the caller navigates to, since
            // nothing else would have told them the session ended. Centralized here instead of at each
            // of those call sites since every authenticated request already passes through this one
            // handler.
            _authenticationStateProvider.NotifyAuthenticationStateChanged();
            return response;
        }

        var refreshedAccessToken = await _tokenStore.GetTokenAsync();
        response.Dispose();
        var retryRequest = await CloneRequestAsync(request);
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshedAccessToken);
        return await base.SendAsync(retryRequest, cancellationToken);
    }

    private async Task AttachAccessTokenAsync(HttpRequestMessage request)
    {
        var token = await _tokenStore.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    /// <summary>
    /// HttpRequestMessage can only be sent once, so retrying the original request requires a copy -
    /// method, URI, content, headers, and options included.
    /// </summary>
    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        if (request.Content is not null)
        {
            var contentBytes = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(contentBytes);
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.Add(header.Key, header.Value);
            }
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in request.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        }

        return clone;
    }
}
