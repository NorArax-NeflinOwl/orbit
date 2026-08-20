using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Orbit.Contracts.Users;

namespace Orbit.Web.Services;

/// <summary>
/// Attaches the signed-in user's access token to every outgoing request made through it. If the API
/// rejects a request with 401 (the access token expired), transparently exchanges the stored refresh
/// token for a new access token through a separate, unauthenticated HttpClient - reusing this handler
/// for that call would recurse into this same retry logic - and retries the original request once.
/// </summary>
public sealed class AuthorizationMessageHandler(TokenStore tokenStore, HttpClient refreshHttpClient) : DelegatingHandler
{
    private readonly TokenStore _tokenStore = tokenStore;
    private readonly HttpClient _refreshHttpClient = refreshHttpClient;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await AttachAccessTokenAsync(request);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        var refreshedAccessToken = await TryRefreshAccessTokenAsync(cancellationToken);
        if (refreshedAccessToken is null)
        {
            return response;
        }

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

    private async Task<string?> TryRefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        var refreshToken = await _tokenStore.GetRefreshTokenAsync();
        if (string.IsNullOrEmpty(refreshToken))
        {
            return null;
        }

        var response = await _refreshHttpClient.PostAsJsonAsync("api/auth/refresh", new RefreshTokenRequest(refreshToken), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // The refresh token is invalid or expired too - there is no way back into a signed-in state
            // without the user logging in again, so both tokens are cleared.
            await _tokenStore.ClearTokenAsync();
            return null;
        }

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: cancellationToken);
        if (authResponse is null)
        {
            return null;
        }

        await _tokenStore.SetTokensAsync(authResponse.Token, authResponse.RefreshToken);
        return authResponse.Token;
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
