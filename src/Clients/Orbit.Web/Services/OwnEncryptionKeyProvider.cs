using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// Makes sure this browser has a local ECDH key pair for end-to-end-encrypted chat (generating one via
/// e2eeChat.js on first use) and that Orbit.Api has the matching public key on file, so other users can
/// find it. Chat.razor depends on this having run before any message can be encrypted or decrypted.
/// </summary>
public sealed class OwnEncryptionKeyProvider
{
    private readonly IJSRuntime _jsRuntime;
    private readonly UsersApiClient _usersApiClient;
    private string? _publicKeyBase64;

    public OwnEncryptionKeyProvider(IJSRuntime jsRuntime, UsersApiClient usersApiClient)
    {
        _jsRuntime = jsRuntime;
        _usersApiClient = usersApiClient;
    }

    /// <summary>
    /// Idempotent within a browser session: the underlying key never changes once generated (it's
    /// persisted in IndexedDB by e2eeChat.js), so repeated calls just return the cached value after the
    /// first one actually talks to the browser and the API.
    /// </summary>
    public async Task<string> EnsurePublicKeyAsync()
    {
        if (_publicKeyBase64 is not null)
        {
            return _publicKeyBase64;
        }

        await using var cryptoModule = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/e2eeChat.js");
        _publicKeyBase64 = await cryptoModule.InvokeAsync<string>("ensureOwnPublicKey");
        await _usersApiClient.SetPublicKeyAsync(_publicKeyBase64);
        return _publicKeyBase64;
    }
}
