using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Users;

namespace Orbit.Mobile.Api;

/// <summary>
/// What the server knows about the user's chat key backup - and, crucially, whether it actually said so.
/// </summary>
public enum BackupLookupOutcome
{
    /// <summary>A backup exists and came back.</summary>
    Found,

    /// <summary>
    /// The server answered that there is none. This account has never backed a key up, so generating a
    /// fresh one loses nothing.
    /// </summary>
    ServerHasNone,

    /// <summary>
    /// The question could not be asked at all. <b>Not</b> the same as there being none: acting on this as
    /// though the account had no backup would replace a key that does exist, and no copy of it survives.
    /// </summary>
    CouldNotAsk
}

public sealed record BackupLookup(BackupLookupOutcome Outcome, WrappedPrivateKeyDto? Backup = null);

/// <summary>
/// The user's published public key and the password-wrapped backup of the matching private key. The
/// server stores both and can read neither.
/// </summary>
public sealed class EncryptionKeyClient
{
    private readonly HttpClient _httpClient;

    public EncryptionKeyClient(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>
    /// Never throws for a network failure, because the difference between "there is no backup" and "I
    /// could not find out" is the whole point of this method - a caller that saw an exception would have
    /// to guess, and guessing wrong costs the user their chat history.
    /// </summary>
    public async Task<BackupLookup> FindBackupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("api/users/me/encryption-key", cancellationToken);

            // 204, deliberately, rather than 404: having no backup is a normal state, not a missing page.
            if (response.StatusCode is HttpStatusCode.NoContent)
            {
                return new BackupLookup(BackupLookupOutcome.ServerHasNone);
            }

            if (!response.IsSuccessStatusCode)
            {
                return new BackupLookup(BackupLookupOutcome.CouldNotAsk);
            }

            var backup = await response.Content.ReadFromJsonAsync<WrappedPrivateKeyDto>(cancellationToken);
            return backup is null
                ? new BackupLookup(BackupLookupOutcome.ServerHasNone)
                : new BackupLookup(BackupLookupOutcome.Found, backup);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                          && !cancellationToken.IsCancellationRequested)
        {
            return new BackupLookup(BackupLookupOutcome.CouldNotAsk);
        }
    }

    /// <summary>
    /// Publishes the public key and the wrapped private key together, because the two must always agree -
    /// a backup that doesn't match the published public key is useless to whoever restores it.
    /// </summary>
    public async Task PublishAsync(
        string publicKeyBase64, WrappedPrivateKeyDto wrappedPrivateKey, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            "api/users/me/encryption-key", new SetEncryptionKeyRequest(publicKeyBase64, wrappedPrivateKey), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task PublishPublicKeyAsync(string publicKeyBase64, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            "api/users/me/public-key", new SetPublicKeyRequest(publicKeyBase64), cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
