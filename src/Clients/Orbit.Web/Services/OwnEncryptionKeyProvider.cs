using System.Net;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using Orbit.Contracts.Users;

namespace Orbit.Web.Services;

/// <summary>
/// Makes sure this browser has a local ECDH key pair for end-to-end-encrypted chat and that Orbit.Api has
/// the matching public key on file, so other users can find it. Chat.razor depends on this having run
/// before any message can be encrypted or decrypted.
///
/// The private key only ever exists in plaintext in this browser's IndexedDB (see e2eeChat.js) - Orbit.Api
/// stores nothing but a password-encrypted backup (<see cref="WrappedPrivateKeyDto"/>) it can never read.
/// <see cref="EnsurePublicKeyAsync"/> never creates or restores a key itself, because doing so without the
/// account password would either silently orphan an existing backup or generate a key nobody could ever
/// back up; only <see cref="UnlockOrCreateAsync"/>, called right after a fresh login or registration while
/// the password is still on hand, is allowed to do that.
///
/// Key material is scoped to the signed-in user's ID, both in the cache fields below and in e2eeChat.js's
/// IndexedDB records, so two different accounts signing into the same browser get their own key pairs
/// instead of silently sharing (or overwriting) one another's.
/// </summary>
public sealed class OwnEncryptionKeyProvider
{
    private readonly IJSRuntime _jsRuntime;
    private readonly UsersApiClient _usersApiClient;
    private readonly OrbitAuthenticationStateProvider _authenticationStateProvider;
    private Guid? _cachedForUserId;
    private string? _publicKeyBase64;

    public OwnEncryptionKeyProvider(
        IJSRuntime jsRuntime, UsersApiClient usersApiClient, OrbitAuthenticationStateProvider authenticationStateProvider)
    {
        _jsRuntime = jsRuntime;
        _usersApiClient = usersApiClient;
        _authenticationStateProvider = authenticationStateProvider;
    }

    /// <summary>
    /// Idempotent for a given signed-in user: the underlying key never changes once generated, so
    /// repeated calls for the same user just return the cached value after the first one actually talks
    /// to the browser and the API. If a different user ID is signed in than on the previous call - e.g.
    /// someone else logging into this same browser - the cache is bypassed and that user's own key pair
    /// is looked up instead.
    /// </summary>
    /// <exception cref="EncryptionKeyLockedException">
    /// This browser has no local private key for the signed-in user - most often because it was
    /// generated in a different browser or profile, or this one's storage was cleared. Callers should
    /// direct the user to sign in again (see <see cref="UnlockOrCreateAsync"/>) rather than treat this as
    /// a transient failure.
    /// </exception>
    public async Task<string> EnsurePublicKeyAsync()
    {
        var ownUserId = await GetOwnUserIdAsync();
        if (_publicKeyBase64 is not null && _cachedForUserId == ownUserId)
        {
            return _publicKeyBase64;
        }

        await using var cryptoModule = await ImportCryptoModuleAsync();
        if (!await cryptoModule.InvokeAsync<bool>("hasOwnPrivateKey", ownUserId))
        {
            throw new EncryptionKeyLockedException();
        }

        _publicKeyBase64 = await cryptoModule.InvokeAsync<string>("ensureOwnPublicKey", ownUserId);
        _cachedForUserId = ownUserId;
        await _usersApiClient.SetPublicKeyAsync(_publicKeyBase64);
        return _publicKeyBase64;
    }

    /// <summary>
    /// Called right after a successful login or registration, while the plaintext password is still on
    /// hand - the only time a password-encrypted backup can be restored or created. If this browser
    /// already has a private key, it's left untouched and just opportunistically backed up, so an
    /// already-working browser also ends up covered by this feature on its next sign-in. Otherwise,
    /// restores the account's private key from its server-side backup if one exists and this password can
    /// decrypt it, or generates a brand-new key pair if it doesn't - the same fallback this browser had no
    /// choice but to take before this feature existed.
    /// </summary>
    public async Task UnlockOrCreateAsync(string password)
    {
        var ownUserId = await GetOwnUserIdAsync();
        await using var cryptoModule = await ImportCryptoModuleAsync();

        if (!await cryptoModule.InvokeAsync<bool>("hasOwnPrivateKey", ownUserId))
        {
            await RestoreOrGeneratePrivateKeyAsync(cryptoModule, ownUserId, password);
        }

        _publicKeyBase64 = await cryptoModule.InvokeAsync<string>("ensureOwnPublicKey", ownUserId);
        _cachedForUserId = ownUserId;

        await BackUpPrivateKeyAsync(cryptoModule, ownUserId, password);
    }

    private async Task RestoreOrGeneratePrivateKeyAsync(IJSObjectReference cryptoModule, Guid ownUserId, string password)
    {
        var wrappedPrivateKey = await TryGetWrappedPrivateKeyAsync();
        if (wrappedPrivateKey is not null)
        {
            var restoredPublicKeyBase64 = await cryptoModule.InvokeAsync<string?>(
                "restoreOwnPrivateKeyFromBackup", ownUserId, password, wrappedPrivateKey);
            if (restoredPublicKeyBase64 is not null)
            {
                return;
            }
        }

        // No backup on the server, the backup couldn't be fetched, or it couldn't be decrypted (corrupted,
        // or wrapped under a different password than the one that was just used to sign in) - fall back to
        // a brand-new key pair rather than leaving this browser without one at all.
        await cryptoModule.InvokeAsync<string>("ensureOwnPublicKey", ownUserId);
    }

    /// <summary>
    /// Treats a failure to reach the backup (API unreachable, a transient server error, ...) the same as
    /// "no backup exists" - a fresh sign-in should never end up locked out of chat (see
    /// EnsurePublicKeyAsync) just because this one best-effort lookup couldn't complete, when falling back
    /// to a brand-new key pair is always a safe, working alternative here.
    /// </summary>
    private async Task<WrappedPrivateKeyDto?> TryGetWrappedPrivateKeyAsync()
    {
        try
        {
            return await _usersApiClient.GetWrappedPrivateKeyAsync();
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task BackUpPrivateKeyAsync(IJSObjectReference cryptoModule, Guid ownUserId, string password)
    {
        var wrappedPrivateKey = await cryptoModule.InvokeAsync<WrappedPrivateKeyDto?>(
            "wrapOwnPrivateKeyWithPassword", ownUserId, password);
        if (wrappedPrivateKey is null)
        {
            // This browser's private key predates extractable keys and can never be exported - fall back
            // to publishing just the public key, as before this feature existed, rather than leaving the
            // server's copy stale.
            await _usersApiClient.SetPublicKeyAsync(_publicKeyBase64!);
            return;
        }

        await _usersApiClient.SetEncryptionKeyAsync(_publicKeyBase64!, wrappedPrivateKey);
    }

    private ValueTask<IJSObjectReference> ImportCryptoModuleAsync()
        => _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/e2eeChat.js");

    /// <summary>
    /// Calls <see cref="OrbitAuthenticationStateProvider.GetAuthenticationStateAsync"/> directly rather
    /// than <see cref="OrbitAuthenticationStateProvider.TryGetCurrentUserIdAsync"/> - both try a silent
    /// token refresh before giving up on a locally-expired access token, but the latter also calls
    /// NotifyAuthenticationStateChanged() before returning, which would recurse back into this method:
    /// that notification is exactly what makes MainLayout.EnsureEncryptionKeyIfAuthenticatedAsync (one of
    /// this method's own callers, via EnsurePublicKeyAsync) run in the first place, since it's registered
    /// as an AuthenticationStateChanged handler - notifying again from in here would just trigger another
    /// call to itself, forever, with no delay. Only throws the "session invalid, go sign in again" signal
    /// every caller here already handles (HttpRequestException/Unauthorized - see EnsurePublicKeyAsync's
    /// callers in MainLayout and Chat.razor) once that refresh genuinely fails.
    /// </summary>
    private async Task<Guid> GetOwnUserIdAsync()
    {
        var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.FindFirst("sub") is { } subjectClaim
            ? Guid.Parse(subjectClaim.Value)
            : throw new HttpRequestException("No signed-in user.", null, HttpStatusCode.Unauthorized);
    }
}
