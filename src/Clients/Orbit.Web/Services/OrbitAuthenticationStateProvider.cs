using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace Orbit.Web.Services;

/// <summary>
/// Builds Blazor's authentication state from the JWT held in <see cref="TokenStore"/>. The token's
/// signature is already verified server-side on every API call; this only reads its claims to drive
/// the UI (showing the display name, deciding whether to show the login page).
/// </summary>
public sealed class OrbitAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState AnonymousState = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly TokenStore _tokenStore;

    public OrbitAuthenticationStateProvider(TokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokenStore.GetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return AnonymousState;
        }

        var identity = new ClaimsIdentity(ParseClaimsFromJwt(token), authenticationType: "jwt", nameType: "name", roleType: null);
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    /// <summary>
    /// Tells Blazor the sign-in state changed, so components depending on it re-render immediately
    /// instead of waiting for the next navigation.
    /// </summary>
    public void NotifyAuthenticationStateChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(jwt.Split('.')[1]));
        var payloadProperties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson)
            ?? new Dictionary<string, JsonElement>();

        return payloadProperties.Select(property => new Claim(property.Key, property.Value.ToString()));
    }

    private static byte[] Base64UrlDecode(string base64Url)
    {
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');
        var paddingNeeded = (4 - base64.Length % 4) % 4;
        base64 = base64.PadRight(base64.Length + paddingNeeded, '=');
        return Convert.FromBase64String(base64);
    }
}
