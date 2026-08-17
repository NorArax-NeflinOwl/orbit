using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// Makes sure this browser has a local ECDH key pair for end-to-end-encrypted chat (generating one via
/// e2eeChat.js on first use) and that Orbit.Api has the matching public key on file, so other users can
/// find it. Chat.razor depends on this having run before any message can be encrypted or decrypted.
///
/// Key material is scoped to the signed-in user's ID, both in the cache field below and in
/// e2eeChat.js's IndexedDB records, so two different accounts signing into the same browser get their
/// own key pairs instead of silently sharing (or overwriting) one another's.
/// </summary>
public sealed class OwnEncryptionKeyProvider
{
    private readonly IJSRuntime _jsRuntime;
    private readonly UsersApiClient _usersApiClient;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private Guid? _cachedForUserId;
    private string? _publicKeyBase64;

    public OwnEncryptionKeyProvider(
        IJSRuntime jsRuntime, UsersApiClient usersApiClient, AuthenticationStateProvider authenticationStateProvider)
    {
        _jsRuntime = jsRuntime;
        _usersApiClient = usersApiClient;
        _authenticationStateProvider = authenticationStateProvider;
    }

    /// <summary>
    /// Idempotent for a given signed-in user: the underlying key never changes once generated (it's
    /// persisted in IndexedDB by e2eeChat.js, keyed by that user's ID), so repeated calls for the same
    /// user just return the cached value after the first one actually talks to the browser and the API.
    /// If a different user ID is signed in than on the previous call - e.g. someone else logging into
    /// this same browser - the cache is bypassed and that user's own key pair is looked up instead.
    /// </summary>
    public async Task<string> EnsurePublicKeyAsync()
    {
        var ownUserId = await GetOwnUserIdAsync();
        if (_publicKeyBase64 is not null && _cachedForUserId == ownUserId)
        {
            return _publicKeyBase64;
        }

        await using var cryptoModule = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/e2eeChat.js");
        _publicKeyBase64 = await cryptoModule.InvokeAsync<string>("ensureOwnPublicKey", ownUserId);
        _cachedForUserId = ownUserId;
        await _usersApiClient.SetPublicKeyAsync(_publicKeyBase64);
        return _publicKeyBase64;
    }

    private async Task<Guid> GetOwnUserIdAsync()
    {
        var authenticationState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        return Guid.Parse(authenticationState.User.FindFirst("sub")!.Value);
    }
}
