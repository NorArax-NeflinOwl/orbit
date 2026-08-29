using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Foundation;
using Orbit.Mobile.Authentication;

namespace Orbit.Maui.Platform;

/// <summary>
/// Google sign-in on iOS, as the authorization-code flow with PKCE that Google requires of a native
/// app: a system browser sheet, a redirect back on the app's own URL scheme, and a token exchange with
/// no client secret - a secret shipped inside an app is not one.
///
/// <para>
/// Two things have to be in the bundle before this can work, and neither can be invented here:
/// <c>GIDClientID</c> in Info.plist, the iOS OAuth client id from the Google Cloud Console; and that
/// id reversed, registered under <c>CFBundleURLTypes</c>, so iOS knows to hand the redirect back to
/// Orbit. Without the first, <see cref="IsConfigured"/> is false and the sign-in screen hides the
/// button rather than opening a sheet that could only end in an error.
/// </para>
///
/// <para>
/// The audience side is the server's: it accepts a token only if the id it was minted for is one of
/// the ids it was configured with - see GoogleAuthSettings and §4.3 of info/orbit-maui-plan.md, which
/// is about exactly this mismatch being invisible until sign-in fails.
/// </para>
/// </summary>
public sealed class GoogleSignIn : IGoogleSignIn
{
    /// <summary>Where the sheet sends the reader, and where Google issues the token.</summary>
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    /// <summary>Info.plist's key for the iOS OAuth client id, the name Google's own SDK reads.</summary>
    private const string ClientIdKey = "GIDClientID";

    /// <summary>openid is what makes Google issue an id_token at all; the other two fill in the account.</summary>
    private const string Scope = "openid email profile";

    private readonly HttpClient _httpClient;

    public GoogleSignIn(HttpClient httpClient) => _httpClient = httpClient;

    public bool IsConfigured => ClientId is not null;

    private static string? ClientId
        => NSBundle.MainBundle.ObjectForInfoDictionary(ClientIdKey)?.ToString() is { Length: > 0 } id
            ? id
            : null;

    public async Task<GoogleSignInResult> RequestIdTokenAsync(CancellationToken cancellationToken = default)
    {
        if (ClientId is not { } clientId)
        {
            return GoogleSignInResult.NotConfigured;
        }

        // Google's documented redirect for an iOS app: the client id with its dot-separated parts
        // reversed, which is also the URL scheme the bundle must claim.
        var redirectUri = $"{ReverseClientId(clientId)}:/oauth2redirect";
        var verifier = CreateCodeVerifier();

        try
        {
            var authorization = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions
            {
                Url = new Uri(
                    $"{AuthorizationEndpoint}?client_id={Uri.EscapeDataString(clientId)}"
                    + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                    + "&response_type=code"
                    + $"&scope={Uri.EscapeDataString(Scope)}"
                    + $"&code_challenge={Challenge(verifier)}"
                    + "&code_challenge_method=S256"),
                CallbackUrl = new Uri(redirectUri),
                // Nothing here should be signed in on the reader's behalf by a cookie they forgot about.
                PrefersEphemeralWebBrowserSession = true
            });

            if (!authorization.Properties.TryGetValue("code", out var code))
            {
                return GoogleSignInResult.Failed;
            }

            return await ExchangeAsync(clientId, code, redirectUri, verifier, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            // The sheet was dismissed. An answer, not a fault.
            return GoogleSignInResult.Cancelled;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"Google sign-in failed: {exception}");
            return GoogleSignInResult.Failed;
        }
    }

    /// <summary>
    /// Trades the one-time code for the identity token. No client secret: a native app cannot keep one,
    /// which is what the verifier below replaces.
    /// </summary>
    private async Task<GoogleSignInResult> ExchangeAsync(
        string clientId, string code, string redirectUri, string verifier, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync(
            TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["code"] = code,
                ["code_verifier"] = verifier,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri
            }),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return GoogleSignInResult.Failed;
        }

        var tokens = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken);
        return tokens?.IdToken is { Length: > 0 } idToken
            ? GoogleSignInResult.Succeeded(idToken)
            : GoogleSignInResult.Failed;
    }

    private static string ReverseClientId(string clientId)
        => string.Join('.', clientId.Split('.').Reverse());

    /// <summary>
    /// The secret half of PKCE: a random string kept in this process, sent only with the exchange. It
    /// is what stops another app that intercepted the redirect from using the code.
    /// </summary>
    private static string CreateCodeVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string Challenge(string verifier)
        => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record GoogleTokenResponse([property: JsonPropertyName("id_token")] string? IdToken);
}
