using System.Text.Json;
using Microsoft.JSInterop;
using Orbit.Contracts;

namespace Orbit.Web.Services;

/// <summary>
/// Seals the content of a private note or task list in this browser, and opens it again on the way back
/// - the step that lets Orbit.Api store something it can never read (see EncryptedContentDto).
///
/// The key is the one the chat already uses, agreed with the owner's own public key on both sides (see
/// e2eeChat.js's encryptForSelf). That means no second key to generate, back up or restore: a browser
/// that can read your chat can read your private notes, one that can't will be asked for your password
/// the same way, and a password reset that replaces the key pair loses both alike.
/// </summary>
public sealed class PrivateContentSealer
{
    private readonly OwnEncryptionKeyProvider _ownEncryptionKeyProvider;
    private readonly OrbitAuthenticationStateProvider _authenticationStateProvider;
    private readonly IJSRuntime _jsRuntime;

    public PrivateContentSealer(
        OwnEncryptionKeyProvider ownEncryptionKeyProvider,
        OrbitAuthenticationStateProvider authenticationStateProvider,
        IJSRuntime jsRuntime)
    {
        _ownEncryptionKeyProvider = ownEncryptionKeyProvider;
        _authenticationStateProvider = authenticationStateProvider;
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Serializes <paramref name="content"/> and seals it. Throws
    /// <see cref="EncryptionKeyLockedException"/> when this browser holds no key for the signed-in user,
    /// which callers surface as the same "unlock to continue" prompt chat uses - saving under a key that
    /// isn't there would produce content nobody could ever open.
    /// </summary>
    public async Task<EncryptedContentDto> SealAsync<TContent>(TContent content, CancellationToken cancellationToken = default)
    {
        var ownUserId = await RequireOwnUserIdAsync();
        await _ownEncryptionKeyProvider.EnsurePublicKeyAsync();

        await using var cryptoModule = await ImportCryptoModuleAsync();
        var sealedContent = await cryptoModule.InvokeAsync<SealedContent>(
            "encryptForSelf", cancellationToken, ownUserId, JsonSerializer.Serialize(content));

        return new EncryptedContentDto(sealedContent.CiphertextBase64, sealedContent.NonceBase64);
    }

    /// <summary>
    /// Opens what <see cref="SealAsync"/> produced. Returns default when the content can't be opened -
    /// content sealed under a key pair that has since been replaced, most often - so a list can show one
    /// unreadable item rather than failing whole.
    /// </summary>
    public async Task<TContent?> OpenAsync<TContent>(EncryptedContentDto encryptedContent, CancellationToken cancellationToken = default)
    {
        var ownUserId = await RequireOwnUserIdAsync();

        await using var cryptoModule = await ImportCryptoModuleAsync();
        var plainText = await cryptoModule.InvokeAsync<string?>(
            "decryptForSelf", cancellationToken, ownUserId, encryptedContent.Ciphertext, encryptedContent.Nonce);

        return plainText is null ? default : JsonSerializer.Deserialize<TContent>(plainText);
    }

    private async Task<Guid> RequireOwnUserIdAsync()
        => await _authenticationStateProvider.TryGetCurrentUserIdAsync()
            // No signed-in user means no key to seal with - the same dead end as a browser missing the
            // key, and the same prompt fixes it.
            ?? throw new EncryptionKeyLockedException();

    private ValueTask<IJSObjectReference> ImportCryptoModuleAsync()
        => _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/e2eeChat.js");

    /// <summary>Shape returned by e2eeChat.js's encryptForSelf - matched by camelCase property name.</summary>
    private sealed record SealedContent(string CiphertextBase64, string NonceBase64);
}
