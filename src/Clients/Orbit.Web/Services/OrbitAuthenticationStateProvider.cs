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
    private readonly TokenRefreshService _tokenRefreshService;

    public OrbitAuthenticationStateProvider(TokenStore tokenStore, TokenRefreshService tokenRefreshService)
    {
        _tokenStore = tokenStore;
        _tokenRefreshService = tokenRefreshService;
    }

    /// <summary>
    /// Tries a silent refresh before settling on anonymous, so a locally-expired (or missing) access
    /// token never has to mean "signed out" while the refresh token could still keep the session alive.
    /// This is the single choke point that decides authentication state for the whole app - the cascading
    /// value AuthorizeRouteView gates every route on (including a cold boot / full page reload, e.g.
    /// reopening a backgrounded PWA tab past the access token's 15-minute lifetime) and MainLayout's own
    /// initial "am I signed in" read both come from here, so fixing it here covers those automatically
    /// instead of needing every caller to know to retry. TokenRefreshService.TryRefreshAsync already
    /// no-ops cheaply (no network call) when there's no refresh token to redeem, so calling it
    /// unconditionally here - even for a page that was never signed in at all - costs nothing but one
    /// local storage read.
    /// </summary>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var localState = await GetLocalAuthenticationStateAsync();
        if (localState.User.Identity?.IsAuthenticated == true)
        {
            return localState;
        }

        return await _tokenRefreshService.TryRefreshAsync() ? await GetLocalAuthenticationStateAsync() : AnonymousState;
    }

    private async Task<AuthenticationState> GetLocalAuthenticationStateAsync()
    {
        var token = await _tokenStore.GetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return AnonymousState;
        }

        var claims = ParseClaimsFromJwt(token).ToList();
        if (IsExpired(claims))
        {
            return AnonymousState;
        }

        var identity = new ClaimsIdentity(claims, authenticationType: "jwt", nameType: "name", roleType: null);
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    /// <summary>
    /// Tells Blazor the sign-in state changed, so components depending on it re-render immediately
    /// instead of waiting for the next navigation.
    /// </summary>
    public void NotifyAuthenticationStateChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    /// <summary>
    /// The signed-in user's id, or null if there genuinely isn't one - tries the stored token locally
    /// first and, only if that's missing or expired, falls back to GetAuthenticationStateAsync's silent
    /// refresh. Every editor/chat page that needs "who am I" on its own initial load (rather than through
    /// an API call that would go through AuthorizationMessageHandler anyway) should call this instead of
    /// reading the "sub" claim directly.
    ///
    /// Callers of this method are themselves the routed content AuthorizeRouteView renders, and Blazor
    /// remounts (disposes and reinitializes) that content every time the cascading auth state changes -
    /// i.e. every NotifyAuthenticationStateChanged call. Calling Notify unconditionally here used to mean
    /// every call re-triggered the very OnInitializedAsync it was called from, forever, with no delay -
    /// visible as a frozen tab within seconds of opening Chat/NoteEditor/TaskEditor/CalendarEventEditor.
    /// Only notifying when the local read actually needed a refresh keeps the common case (a still-valid
    /// token) a no-op remount-wise, while still updating the sidebar/route gating promptly on the two
    /// paths where something genuinely changed: a recovered session (remounts once more, this time with a
    /// now-locally-valid token, so it terminates immediately) or a genuinely-dead one (AuthorizeRouteView
    /// switches to NotAuthorized instead of remounting this component at all).
    /// </summary>
    public async Task<Guid?> TryGetCurrentUserIdAsync()
    {
        var localState = await GetLocalAuthenticationStateAsync();
        if (localState.User.FindFirst("sub") is { } localClaim)
        {
            return Guid.Parse(localClaim.Value);
        }

        var refreshedState = await GetAuthenticationStateAsync();
        NotifyAuthenticationStateChanged();
        return refreshedState.User.FindFirst("sub") is { } refreshedClaim ? Guid.Parse(refreshedClaim.Value) : null;
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(jwt.Split('.')[1]));
        var payloadProperties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson)
            ?? new Dictionary<string, JsonElement>();

        return payloadProperties.Select(property => new Claim(property.Key, property.Value.ToString()));
    }

    /// <summary>
    /// The server already rejects an expired token on every API call (see AuthorizationMessageHandler,
    /// which then tries a silent refresh), but checking the "exp" claim here too means the UI itself -
    /// the nav bar in MainLayout, [Authorize] route gating - never treats a stale token as a real session
    /// in the first place, instead of only finding out once the first request fails. This also covers a
    /// token that survives in localStorage across a local database reset (e.g. during development): it
    /// looks unexpired and well-formed, and was otherwise indistinguishable from a genuine session until
    /// something finally tried to use it.
    /// </summary>
    private static bool IsExpired(IEnumerable<Claim> claims)
    {
        var expiresAtClaim = claims.FirstOrDefault(claim => claim.Type == "exp");
        if (expiresAtClaim is null || !long.TryParse(expiresAtClaim.Value, out var expiresAtUnixSeconds))
        {
            return false;
        }

        return DateTimeOffset.FromUnixTimeSeconds(expiresAtUnixSeconds) <= DateTimeOffset.UtcNow;
    }

    private static byte[] Base64UrlDecode(string base64Url)
    {
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');
        var paddingNeeded = (4 - base64.Length % 4) % 4;
        base64 = base64.PadRight(base64.Length + paddingNeeded, '=');
        return Convert.FromBase64String(base64);
    }
}
