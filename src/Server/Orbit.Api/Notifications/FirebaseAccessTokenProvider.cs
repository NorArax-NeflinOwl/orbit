using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Orbit.Api.Notifications;

/// <summary>
/// Trades the Firebase service account key for an OAuth access token FCM will accept, and holds on to
/// it until shortly before it expires.
///
/// Written out rather than taken from the Firebase Admin SDK because this is all Orbit needs from it:
/// sign a JWT with the account's private key, POST it to Google's token endpoint, get an hour-long
/// token back. Pulling in the Admin SDK for that would bring a large dependency - and its own
/// initialisation and threading model - to replace about thirty lines.
/// </summary>
public sealed class FirebaseAccessTokenProvider
{
    private const string MessagingScope = "https://www.googleapis.com/auth/firebase.messaging";

    /// <summary>Tokens last an hour; renewing a minute early avoids racing the expiry on a slow request.</summary>
    private static readonly TimeSpan RenewBeforeExpiry = TimeSpan.FromMinutes(1);

    private readonly IOptionsMonitor<FirebaseSettings> _settings;
    private readonly HttpClient _httpClient;

    // One token serves every send, so concurrent notifications must not each mint their own.
    private readonly SemaphoreSlim _renewalLock = new(1, 1);
    private CachedToken? _cached;

    public FirebaseAccessTokenProvider(IOptionsMonitor<FirebaseSettings> settings, HttpClient httpClient)
    {
        _settings = settings;
        _httpClient = httpClient;
    }

    /// <summary>The Firebase project id from the same key file, which FCM's send URL is scoped to.</summary>
    public string ReadProjectId() => ReadServiceAccount().ProjectId;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cached is { } cached && DateTimeOffset.UtcNow < cached.ExpiresAtUtc - RenewBeforeExpiry)
        {
            return cached.AccessToken;
        }

        await _renewalLock.WaitAsync(cancellationToken);
        try
        {
            // Re-check inside the lock: whoever was ahead in the queue has already renewed it.
            if (_cached is { } current && DateTimeOffset.UtcNow < current.ExpiresAtUtc - RenewBeforeExpiry)
            {
                return current.AccessToken;
            }

            var renewed = await RequestTokenAsync(cancellationToken);
            _cached = renewed;
            return renewed.AccessToken;
        }
        finally
        {
            _renewalLock.Release();
        }
    }

    private async Task<CachedToken> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var account = ReadServiceAccount();
        var assertion = BuildSignedAssertion(account);

        using var response = await _httpClient.PostAsync(
            account.TokenUri,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion
            }),
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Google refused the Firebase service account key ({(int)response.StatusCode}). " +
                "The key may have been revoked - see secrets/README.md.");
        }

        var token = JsonDocument.Parse(body).RootElement;
        return new CachedToken(
            token.GetProperty("access_token").GetString()!,
            DateTimeOffset.UtcNow.AddSeconds(token.GetProperty("expires_in").GetInt32()));
    }

    private static string BuildSignedAssertion(ServiceAccount account)
    {
        var issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = Base64Url(Encoding.UTF8.GetBytes("""{"alg":"RS256","typ":"JWT"}"""));
        var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = account.ClientEmail,
            scope = MessagingScope,
            aud = account.TokenUri,
            iat = issuedAt,
            exp = issuedAt + 3600
        }));

        var signingInput = $"{header}.{claims}";
        using var rsa = RSA.Create();
        rsa.ImportFromPem(account.PrivateKeyPem);
        var signature = Base64Url(
            rsa.SignData(Encoding.UTF8.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        return $"{signingInput}.{signature}";
    }

    private ServiceAccount ReadServiceAccount()
    {
        var path = _settings.CurrentValue.ServiceAccountKeyPath;
        var key = JsonDocument.Parse(File.ReadAllText(path)).RootElement;
        return new ServiceAccount(
            key.GetProperty("project_id").GetString()!,
            key.GetProperty("client_email").GetString()!,
            key.GetProperty("token_uri").GetString()!,
            key.GetProperty("private_key").GetString()!);
    }

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record ServiceAccount(string ProjectId, string ClientEmail, string TokenUri, string PrivateKeyPem);

    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAtUtc);
}
