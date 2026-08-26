using Orbit.Mobile.Authentication;
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
    private readonly SessionStore _sessionStore;

    public EncryptedChatMessageReader(
        ChatRepository chatRepository, OwnEncryptionKeyProvider encryptionKeyProvider, SessionStore sessionStore)
    {
        _chatRepository = chatRepository;
        _encryptionKeyProvider = encryptionKeyProvider;
        _sessionStore = sessionStore;
    }

    /// <summary>
    /// The conversation with one person, newest last, with anything still queued appended - it was typed
    /// after everything already sent.
    /// </summary>
    /// <exception cref="EncryptionKeyLockedException">
    /// This device has no chat key, so nothing here can be opened. The caller sends the user to the key
    /// gate rather than showing an empty conversation.
    /// </exception>
    /// <param name="theyReadUpToUtc">
    /// How far the other party has read, from the server. Null when nothing of the reader's has been
    /// seen, or when this device has not managed to ask - which is why it is passed in rather than
    /// stored: it is live information, and a remembered one would claim something was read when the
    /// answer is simply unknown.
    /// </param>
    public async Task<IReadOnlyList<ReadableChatMessage>> ReadAsync(
        Guid otherUserId, string otherPartyPublicKeyBase64, DateTimeOffset? theyReadUpToUtc = null,
        CancellationToken cancellationToken = default)
    {
        using var identity = await _encryptionKeyProvider.OpenAsync(cancellationToken);
        var stored = await _chatRepository.GetConversationAsync(otherUserId, cancellationToken);
        var queued = await _chatRepository.GetQueuedForAsync(otherUserId, cancellationToken);

        var conversation = new List<ReadableChatMessage>(stored.Count + queued.Count);
        foreach (var message in stored)
        {
            var isMine = message.SenderUserId != otherUserId;
            conversation.Add(new ReadableChatMessage(
                isMine,
                Text: identity.Decrypt(otherPartyPublicKeyBase64, new EncryptedText(message.CiphertextBase64, message.NonceBase64)),
                message.SentAtUtc,
                message.IsEdited,
                IsWaitingToSend: false,
                MessageId: message.Id,
                // Read up to a point, so everything sent at or before it has been seen.
                IsReadByThem: isMine && theyReadUpToUtc is { } readUpTo && message.SentAtUtc <= readUpTo));
        }

        foreach (var message in queued)
        {
            conversation.Add(new ReadableChatMessage(
                IsMine: true, message.Text, message.QueuedAtUtc, IsEdited: false, IsWaitingToSend: true));
        }

        return conversation;
    }

    /// <summary>
    /// A group conversation, read the same way and with one difference that runs through all of it: the
    /// key changes from message to message.
    ///
    /// A group message is stored as one copy per member, each sealed between the sender and that one
    /// recipient (see ChatMessage.CreateForGroup), so there is no single "other party" for the screen.
    /// The reader's own copies are sealed against a recipient's key rather than their own, which is why
    /// the sender's side agrees with whoever the copy was addressed to.
    /// </summary>
    /// <inheritdoc cref="ReadAsync" path="/exception"/>
    public async Task<IReadOnlyList<ReadableChatMessage>> ReadGroupAsync(
        Guid groupId, CancellationToken cancellationToken = default)
    {
        using var identity = await _encryptionKeyProvider.OpenAsync(cancellationToken);
        var ownUserId = await RequireSignedInUserIdAsync();

        var members = (await _chatRepository.FindGroupAsync(groupId, cancellationToken))?.Members ?? [];
        var stored = await _chatRepository.GetGroupConversationAsync(groupId, cancellationToken);
        var queued = await _chatRepository.GetQueuedForGroupAsync(groupId, cancellationToken);

        var conversation = new List<ReadableChatMessage>(stored.Count + queued.Count);
        foreach (var message in stored)
        {
            var isMine = message.SenderUserId == ownUserId;
            var otherPartyUserId = isMine ? message.RecipientUserId : message.SenderUserId;
            conversation.Add(new ReadableChatMessage(
                isMine,
                Open(identity, members, otherPartyUserId, message),
                message.SentAtUtc,
                message.IsEdited,
                IsWaitingToSend: false,
                SenderName: isMine ? "You" : NameOf(members, message.SenderUserId),
                MessageId: message.Id,
                GroupMessageId: message.GroupMessageId));
        }

        foreach (var message in queued)
        {
            conversation.Add(new ReadableChatMessage(
                IsMine: true, message.Text, message.QueuedAtUtc, IsEdited: false, IsWaitingToSend: true, SenderName: "You"));
        }

        return conversation;
    }

    /// <summary>
    /// Null when the other party to this copy has no cached key - they left the group, or their account
    /// is gone - which the screen shows as one unopenable message rather than an empty conversation.
    /// </summary>
    private static string? Open(
        ChatIdentity identity, IReadOnlyList<LocalChatGroupMember> members, Guid otherPartyUserId, LocalChatMessage message)
        => FindMember(members, otherPartyUserId)?.PublicKeyBase64 is { } otherPartyPublicKey
            ? identity.Decrypt(otherPartyPublicKey, new EncryptedText(message.CiphertextBase64, message.NonceBase64))
            : null;

    private static string NameOf(IReadOnlyList<LocalChatGroupMember> members, Guid userId)
        => FindMember(members, userId)?.DisplayName ?? "Someone";

    private static LocalChatGroupMember? FindMember(IReadOnlyList<LocalChatGroupMember> members, Guid userId)
        => members.FirstOrDefault(member => member.UserId == userId);

    private async Task<Guid> RequireSignedInUserIdAsync()
        => await _sessionStore.GetAsync() is { } session
            ? session.UserId
            : throw new EncryptionKeyLockedException();
}
