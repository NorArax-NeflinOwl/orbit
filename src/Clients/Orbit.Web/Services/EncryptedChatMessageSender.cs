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
    public async Task SendAsync(
        Guid ownUserId, Guid recipientUserId, string plainTextContent, bool isShareInvitation = false,
        CancellationToken cancellationToken = default)
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
            new SendMessageRequest(recipientUserId, payload.CiphertextBase64, payload.NonceBase64, isShareInvitation), cancellationToken);
    }


    /// <summary>
    /// Posts to a group by encrypting the same text once for each other member, under the pairwise key
    /// this browser already shares with each of them. The server can't fan out for us - it has no key to
    /// read the text with - so the whole group's public keys are fetched and the work happens here.
    ///
    /// Throws <see cref="InvalidOperationException"/> naming anyone who has never signed in, since there
    /// is no key to encrypt for them and a group message that silently skipped a member would be worse
    /// than one that refuses to send.
    /// </summary>
    public async Task SendToGroupAsync(
        Guid ownUserId, Guid groupId, IReadOnlyList<Guid> otherMemberUserIds, string plainTextContent,
        CancellationToken cancellationToken = default)
    {
        var copies = await SealForEachMemberAsync(ownUserId, otherMemberUserIds, plainTextContent, cancellationToken);
        await _chatApiClient.SendGroupMessageAsync(groupId, copies, cancellationToken);
    }

    /// <summary>
    /// Rewrites one group message to new text. The same fan-out as sending, because every copy is
    /// separately encrypted - leaving one behind would show different members different words. False
    /// when the message is gone or was somebody else's to edit.
    /// </summary>
    public async Task<bool> EditGroupMessageAsync(
        Guid ownUserId, Guid groupId, Guid groupMessageId, IReadOnlyList<Guid> otherMemberUserIds, string plainTextContent,
        CancellationToken cancellationToken = default)
    {
        var copies = await SealForEachMemberAsync(ownUserId, otherMemberUserIds, plainTextContent, cancellationToken);
        return await _chatApiClient.EditGroupMessageAsync(groupId, groupMessageId, copies, cancellationToken);
    }

    private async Task<IReadOnlyList<GroupMessageCopyDto>> SealForEachMemberAsync(
        Guid ownUserId, IReadOnlyList<Guid> otherMemberUserIds, string plainTextContent, CancellationToken cancellationToken)
    {
        await _ownEncryptionKeyProvider.EnsurePublicKeyAsync();
        await using var cryptoModule = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/e2eeChat.js");

        var copies = new List<GroupMessageCopyDto>(otherMemberUserIds.Count);
        foreach (var memberUserId in otherMemberUserIds)
        {
            var member = await _usersApiClient.GetUserAsync(memberUserId, cancellationToken);
            if (member?.PublicKeyBase64 is null)
            {
                throw new InvalidOperationException(
                    $"User {memberUserId} has no public key on file yet - they must log in at least once before they can be sent group messages.");
            }

            var payload = await cryptoModule.InvokeAsync<EncryptedPayload>(
                "encryptMessage", cancellationToken, ownUserId, member.PublicKeyBase64, plainTextContent);
            copies.Add(new GroupMessageCopyDto(memberUserId, payload.CiphertextBase64, payload.NonceBase64));
        }

        return copies;
    }

    /// <summary>Shape returned by e2eeChat.js's encryptMessage - matched by camelCase property name.</summary>
    private sealed record EncryptedPayload(string CiphertextBase64, string NonceBase64);
}
