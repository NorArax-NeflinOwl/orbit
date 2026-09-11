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
        if (response.StatusCode != HttpStatusCode.Unauthorized || IsSigningIn(request) || RefusesThePassword(request, response))
        {
            return response;
        }

        if (!await _tokenRefreshService.TryRefreshAsync(cancellationToken))
        {
            // The session is genuinely over (the refresh token is invalid or expired too). Every
            // caller that lands here already has its own "catch the 401, NavigateTo /login" handling
            // for its own page content, but none of them think to also invalidate the authentication
            // state - without this, MainLayout's top bar (which listens for exactly this
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

    /// <summary>
    /// The endpoints where a 401 is an answer about the credentials just submitted, not a sign that this
    /// session's access token expired. Everything below this line is about the latter, and doing it to a
    /// sign-in was wrong three times over: it spent the leftover refresh token on a *second* sign-in
    /// attempt nobody made, so the page reported that attempt's refusal instead of the one the reader
    /// made and burned one of the five tries a minute the rate limit allows; and when the leftover token
    /// turned out to be dead too, it cleared the stored tokens and announced the session had ended -
    /// signing out whoever was still signed in in another tab because somebody mistyped a password here.
    ///
    /// The visible symptom was that mistyping a password showed no message at all.
    /// </summary>
    private static readonly string[] SignInPaths = ["/api/auth/login", "/api/auth/register", "/api/auth/google"];

    private static bool IsSigningIn(HttpRequestMessage request)
        => request.RequestUri is { } uri
            && SignInPaths.Contains(uri.AbsolutePath, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The two signed-in requests that prove a password again - deleting the account and changing the
    /// password - answer a wrong one with 401 as well (see UserEndpoints). That is the same mistake the
    /// sign-in paths above were: the refresh-and-retry ran on it, so every mistyped password was sent
    /// twice, spent two of the five tries a minute, and the third mistake came back as the rate limit's
    /// refusal - "couldn't delete your account" - with nothing saying the password was the problem.
    ///
    /// Unlike a sign-in, these are made with a session that may really have expired, so the path alone
    /// cannot decide it. What does is the challenge: the bearer authentication that turns away an expired
    /// token says so in a WWW-Authenticate header, and an endpoint refusing what was typed sends none.
    /// </summary>
    private static readonly (HttpMethod Method, string Path)[] PasswordProvingRequests =
    [
        (HttpMethod.Delete, "/api/users/me"),
        (HttpMethod.Put, "/api/users/me/password")
    ];

    private static bool RefusesThePassword(HttpRequestMessage request, HttpResponseMessage response)
        => request.RequestUri is { } uri
            && PasswordProvingRequests.Any(proving => proving.Method == request.Method
                && string.Equals(proving.Path, uri.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            && !response.Headers.WwwAuthenticate.Any(challenge =>
                string.Equals(challenge.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase));

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
