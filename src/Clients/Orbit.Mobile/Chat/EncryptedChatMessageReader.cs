using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Chat;

/// <summary>
/// Turns the stored conversation into something readable. The counterpart of Orbit.Web's class of the
/// same name, and the only place chat plaintext is produced on this device.
///
/// Reads come from the local database, so a conversation opens with no connection; the sync layer is
/// what keeps that current.
/// </summary>
public sealed class EncryptedChatMessageReader
{
    private readonly ChatRepository _chatRepository;
    private readonly OwnEncryptionKeyProvider _encryptionKeyProvider;

    public EncryptedChatMessageReader(ChatRepository chatRepository, OwnEncryptionKeyProvider encryptionKeyProvider)
    {
        _chatRepository = chatRepository;
        _encryptionKeyProvider = encryptionKeyProvider;
    }

    /// <summary>
    /// The conversation with one person, newest last, with anything still queued appended - it was typed
    /// after everything already sent.
    /// </summary>
    /// <exception cref="EncryptionKeyLockedException">
    /// This device has no chat key, so nothing here can be opened. The caller sends the user to the key
    /// gate rather than showing an empty conversation.
    /// </exception>
    public async Task<IReadOnlyList<ReadableChatMessage>> ReadAsync(
        Guid otherUserId, string otherPartyPublicKeyBase64, CancellationToken cancellationToken = default)
    {
        using var identity = await _encryptionKeyProvider.OpenAsync(cancellationToken);
        var stored = await _chatRepository.GetConversationAsync(otherUserId, cancellationToken);
        var queued = await _chatRepository.GetQueuedForAsync(otherUserId, cancellationToken);

        var conversation = new List<ReadableChatMessage>(stored.Count + queued.Count);
        foreach (var message in stored)
        {
            conversation.Add(new ReadableChatMessage(
                IsMine: message.SenderUserId != otherUserId,
                Text: identity.Decrypt(otherPartyPublicKeyBase64, new EncryptedText(message.CiphertextBase64, message.NonceBase64)),
                message.SentAtUtc,
                message.IsEdited,
                IsWaitingToSend: false));
        }

        foreach (var message in queued)
        {
            conversation.Add(new ReadableChatMessage(
                IsMine: true, message.Text, message.QueuedAtUtc, IsEdited: false, IsWaitingToSend: true));
        }

        return conversation;
    }
}
