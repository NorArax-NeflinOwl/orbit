using System.Net.Http.Headers;

namespace Orbit.Web.Services;

/// <summary>
/// Attaches the signed-in user's JWT to every outgoing request made through it, so individual API
/// clients don't each have to remember to read the token themselves.
/// </summary>
public sealed class AuthorizationMessageHandler : DelegatingHandler
{
    private readonly TokenStore _tokenStore;

    public AuthorizationMessageHandler(TokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokenStore.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
