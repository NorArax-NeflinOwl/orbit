using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Web;

namespace Orbit.Mobile.Authentication;

/// <summary>
/// Sending the reader to Google and getting them back. The one part of signing in with Google that
/// cannot be decided without a device: it opens a browser the app does not own and waits for the system
/// to hand the answer back.
/// </summary>
public interface IWebSignInBrowser
{
    /// <summary>
    /// Where Google is told to send the reader back to. Belongs to the head because it is built from the
    /// application id, and Google will only redirect to the one registered against this app's package
    /// and signing certificate.
    /// </summary>
    Uri CallbackAddress { get; }

    /// <summary>
    /// Which head this is, one of <see cref="Orbit.Core.Mobile.MobilePlatform"/> - which decides which of
    /// the deployment's client ids to ask for, Google issuing one per platform.
    /// </summary>
    string Platform { get; }

    /// <summary>
    /// Opens the address and returns what Google put in the callback, or null when the reader backed out
    /// - which is an ordinary answer rather than a failure.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>?> SignInAsync(
        Uri startAddress, CancellationToken cancellationToken = default);
}

/// <summary>
/// Obtains a Google ID token for the account the reader picks, which is the only thing Orbit's server
/// wants: it verifies the token rather than running a code exchange of its own - see
/// GoogleIdentityVerifier.
///
/// The authorization-code flow with PKCE rather than anything shorter, because this is a public client:
/// the app holds no secret, so the code is bound to a verifier only this run of the flow knows. Without
/// it another app registered for the same redirect could take the code out of the callback and spend it.
///
/// The token that comes back carries the *mobile* client id as its audience, not the browser's. That is
/// why the server keeps a list of accepted client ids rather than one - see GoogleAuthSettings.
/// </summary>
public sealed class GoogleSignIn
{
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    /// <summary>openid is what makes Google return an ID token at all; the other two fill in the account.</summary>
    private const string Scope = "openid email profile";

    private readonly IWebSignInBrowser _browser;
    private readonly HttpClient _httpClient;

    public GoogleSignIn(IWebSignInBrowser browser, HttpClient httpClient)
    {
        _browser = browser;
        _httpClient = httpClient;
    }

    /// <inheritdoc cref="IWebSignInBrowser.Platform"/>
    public string Platform => _browser.Platform;

    /// <summary>
    /// The ID token, or null when the reader backed out or Google refused. Null rather than an exception
    /// for backing out: closing the browser is a choice, not a fault.
    /// </summary>
    public async Task<string?> GetIdTokenAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return null;
        }

        var verifier = CreateCodeVerifier();
        var callback = await _browser.SignInAsync(
            BuildAuthorizationAddress(clientId, verifier), cancellationToken);

        return ReadCode(callback) is { } code
            ? await ExchangeAsync(clientId, code, verifier, cancellationToken)
            : null;
    }

    /// <summary>
    /// The code Google put in the callback, or null when it sent an error instead - which is what a
    /// refused consent screen looks like, and is not worth telling apart from backing out.
    /// </summary>
    internal static string? ReadCode(IReadOnlyDictionary<string, string>? callback)
        => callback is not null && callback.TryGetValue("code", out var code) && !string.IsNullOrWhiteSpace(code)
            ? code
            : null;

    /// <summary>
    /// The address the browser opens. Everything in it is public - the client id included, which is why
    /// there is no secret here to leave out.
    /// </summary>
    internal Uri BuildAuthorizationAddress(string clientId, string codeVerifier)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = clientId;
        query["redirect_uri"] = _browser.CallbackAddress.ToString();
        query["response_type"] = "code";
        query["scope"] = Scope;
        query["code_challenge"] = Challenge(codeVerifier);
        query["code_challenge_method"] = "S256";

        return new Uri($"{AuthorizationEndpoint}?{query}");
    }

    private async Task<string?> ExchangeAsync(
        string clientId, string code, string codeVerifier, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsync(
            TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = clientId,
                ["redirect_uri"] = _browser.CallbackAddress.ToString(),
                ["grant_type"] = "authorization_code",
                ["code_verifier"] = codeVerifier
            }),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var exchanged = await response.Content.ReadFromJsonAsync(
            GoogleTokenSerializerContext.Default.GoogleTokenResponse, cancellationToken);

        return string.IsNullOrWhiteSpace(exchanged?.IdToken) ? null : exchanged.IdToken;
    }

    /// <summary>
    /// The secret half of PKCE: 32 random bytes, spelled the way RFC 7636 allows - unreserved characters
    /// only, which is what base64url without its padding is.
    /// </summary>
    private static string CreateCodeVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string Challenge(string codeVerifier)
        => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>Only the one field matters here; Google sends several more and they are none of Orbit's business.</summary>
internal sealed record GoogleTokenResponse([property: JsonPropertyName("id_token")] string? IdToken);

/// <summary>Source-generated, for the same trimming reason as the chat payloads' context.</summary>
[JsonSerializable(typeof(GoogleTokenResponse))]
internal sealed partial class GoogleTokenSerializerContext : System.Text.Json.Serialization.JsonSerializerContext;
