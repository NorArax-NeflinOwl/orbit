using Orbit.Contracts.Chat;

namespace Orbit.Web.Services;

/// <summary>
/// Gives somebody who joined a group late the conversation they arrived after.
///
/// This has to happen in a browser, and specifically in the browser of a member who can already read the
/// group: every group message is sealed under a pairwise key (see ChatMessage.CreateForGroup), the
/// server holds none of them, and no copy of anything was ever made for the newcomer. So the only way to
/// give them the past is for somebody who has it to open it and seal it again under the key they share
/// with the new member.
///
/// What cannot be opened is left behind rather than sent as something unreadable - see
/// <see cref="ShareWithAsync"/>.
/// </summary>
public sealed class GroupHistorySharing
{
    private readonly ChatApiClient _chatApiClient;
    private readonly EncryptedChatMessageReader _encryptedChatMessageReader;
    private readonly EncryptedChatMessageSender _encryptedChatMessageSender;

    public GroupHistorySharing(
        ChatApiClient chatApiClient, EncryptedChatMessageReader encryptedChatMessageReader,
        EncryptedChatMessageSender encryptedChatMessageSender)
    {
        _chatApiClient = chatApiClient;
        _encryptedChatMessageReader = encryptedChatMessageReader;
        _encryptedChatMessageSender = encryptedChatMessageSender;
    }

    /// <summary>
    /// Returns how many messages the recipient can now read that they could not before. Fewer than the
    /// conversation holds is a normal answer, not a failure: a message sealed under a key pair this
    /// browser has since replaced cannot be opened here either, and one nobody can read is not something
    /// to pass on as ciphertext the newcomer would stare at.
    /// </summary>
    public async Task<int> ShareWithAsync(
        Guid ownUserId, Guid groupId, Guid recipientUserId, CancellationToken cancellationToken = default)
    {
        var conversation = await _chatApiClient.GetGroupConversationAsync(groupId, cancellationToken);

        var readable = new List<HistoryMessageToShare>(conversation.Count);
        foreach (var message in conversation)
        {
            if (message.GroupMessageId is not { } groupMessageId)
            {
                continue;
            }

            // Each copy is between its sender and its recipient, so the key is the pairwise one with
            // whichever of the two isn't the reader - the same rule the group thread decrypts by.
            var otherPartyUserId = message.SenderUserId == ownUserId ? message.RecipientUserId : message.SenderUserId;
            var plainText = await _encryptedChatMessageReader.TryDecryptFromAsync(
                ownUserId, otherPartyUserId, message.CiphertextBase64, message.NonceBase64, cancellationToken);

            if (plainText is not null)
            {
                readable.Add(new HistoryMessageToShare(groupMessageId, plainText));
            }
        }

        if (readable.Count == 0)
        {
            return 0;
        }

        var copies = await _encryptedChatMessageSender.SealHistoryForAsync(
            ownUserId, recipientUserId, readable, cancellationToken);

        return await _chatApiClient.ShareGroupHistoryAsync(groupId, recipientUserId, copies, cancellationToken);
    }
}
