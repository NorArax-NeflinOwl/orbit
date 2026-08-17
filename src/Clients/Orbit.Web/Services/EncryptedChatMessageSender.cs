using Microsoft.JSInterop;
using Orbit.Contracts.Chat;

namespace Orbit.Web.Services;

/// <summary>
/// Encrypts a plaintext string for a given recipient (via e2eeChat.js) and delivers it as an ordinary
/// chat message. Callers outside Chat.razor itself - e.g. sending a structured event-share notice from
/// the calendar editor (see EventShareMessagePayload) - use this instead of duplicating the
/// encrypt-then-send steps Chat.razor performs for user-typed messages.
/// </summary>
public sealed class EncryptedChatMessageSender
{
    private readonly IJSRuntime _jsRuntime;
    private readonly OwnEncryptionKeyProvider _ownEncryptionKeyProvider;
    private readonly UsersApiClient _usersApiClient;
    private readonly ChatApiClient _chatApiClient;

    public EncryptedChatMessageSender(
        IJSRuntime jsRuntime, OwnEncryptionKeyProvider ownEncryptionKeyProvider, UsersApiClient usersApiClient, ChatApiClient chatApiClient)
    {
        _jsRuntime = jsRuntime;
        _ownEncryptionKeyProvider = ownEncryptionKeyProvider;
        _usersApiClient = usersApiClient;
        _chatApiClient = chatApiClient;
    }

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if the recipient hasn't configured encryption yet
    /// (no public key on file) - callers should catch this and show the same "hasn't logged in yet"
    /// message Chat.razor shows when opening a conversation with such a user.
    /// </summary>
    public async Task SendAsync(Guid ownUserId, Guid recipientUserId, string plainTextContent, CancellationToken cancellationToken = default)
    {
        var recipient = await _usersApiClient.GetUserAsync(recipientUserId, cancellationToken);
        if (recipient?.PublicKeyBase64 is null)
        {
            throw new InvalidOperationException($"User {recipientUserId} has no public key on file yet - they must log in at least once first.");
        }

        await _ownEncryptionKeyProvider.EnsurePublicKeyAsync();
        await using var cryptoModule = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/e2eeChat.js");
        var payload = await cryptoModule.InvokeAsync<EncryptedPayload>(
            "encryptMessage", ownUserId, recipient.PublicKeyBase64, plainTextContent);

        await _chatApiClient.SendMessageAsync(
            new SendMessageRequest(recipientUserId, payload.CiphertextBase64, payload.NonceBase64), cancellationToken);
    }

    /// <summary>Shape returned by e2eeChat.js's encryptMessage - matched by camelCase property name.</summary>
    private sealed record EncryptedPayload(string CiphertextBase64, string NonceBase64);
}
