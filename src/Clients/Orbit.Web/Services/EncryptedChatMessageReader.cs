using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// The counterpart to <see cref="EncryptedChatMessageSender"/>: turns one stored ciphertext back into
/// text, given whoever the other party to it was. A group conversation needs this on its own - each
/// message there is a copy between two specific people (see ChatMessage.CreateForGroup), so the key
/// changes from message to message rather than being fixed for the whole window the way Chat.razor's is.
/// </summary>
public sealed class EncryptedChatMessageReader
{
    private readonly UsersApiClient _usersApiClient;
    private readonly OwnEncryptionKeyProvider _ownEncryptionKeyProvider;
    private readonly IJSRuntime _jsRuntime;

    public EncryptedChatMessageReader(
        UsersApiClient usersApiClient, OwnEncryptionKeyProvider ownEncryptionKeyProvider, IJSRuntime jsRuntime)
    {
        _usersApiClient = usersApiClient;
        _ownEncryptionKeyProvider = ownEncryptionKeyProvider;
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Null when the message can't be opened on this device - the other party has no key on file, or the
    /// message was encrypted for a key pair since replaced. Callers render a placeholder for that one
    /// message rather than failing the whole conversation.
    /// </summary>
    public async Task<string?> TryDecryptFromAsync(
        Guid ownUserId, Guid otherPartyUserId, string ciphertextBase64, string nonceBase64, CancellationToken cancellationToken = default)
    {
        var otherParty = await _usersApiClient.GetUserAsync(otherPartyUserId, cancellationToken);
        if (otherParty?.PublicKeyBase64 is null)
        {
            return null;
        }

        await _ownEncryptionKeyProvider.EnsurePublicKeyAsync();
        await using var cryptoModule = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/e2eeChat.js");
        return await cryptoModule.InvokeAsync<string?>(
            "decryptMessage", cancellationToken, ownUserId, otherParty.PublicKeyBase64, ciphertextBase64, nonceBase64);
    }
}
