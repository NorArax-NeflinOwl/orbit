using Orbit.Mobile.Authentication;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;

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
    private readonly Translations _translations;

    public EncryptedChatMessageReader(
        ChatRepository chatRepository, OwnEncryptionKeyProvider encryptionKeyProvider, SessionStore sessionStore,
        Translations translations)
    {
        _chatRepository = chatRepository;
        _encryptionKeyProvider = encryptionKeyProvider;
        _sessionStore = sessionStore;
        _translations = translations;
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
            var opened = Open(
                identity, otherPartyPublicKeyBase64, new EncryptedText(message.CiphertextBase64, message.NonceBase64));

            conversation.Add(new ReadableChatMessage(
                isMine,
                opened.Text,
                message.SentAtUtc,
                message.IsEdited,
                IsWaitingToSend: false,
                MessageId: message.Id,
                // Read up to a point, so everything sent at or before it has been seen.
                IsReadByThem: isMine && theyReadUpToUtc is { } readUpTo && message.SentAtUtc <= readUpTo,
                ForwardedFromDisplayName: opened.ForwardedFromDisplayName,
                Invitation: opened.Invitation,
                EditAccessRequest: opened.EditAccessRequest)
            {
                QuotedMessageId = opened.QuotedMessageId,
                QuotedPreview = opened.QuotedPreview
            });
        }

        foreach (var message in queued)
        {
            var opened = Read(message.Text);
            conversation.Add(new ReadableChatMessage(
                IsMine: true, opened.Text, message.QueuedAtUtc, IsEdited: false, IsWaitingToSend: true,
                ForwardedFromDisplayName: opened.ForwardedFromDisplayName,
                Invitation: opened.Invitation,
                EditAccessRequest: opened.EditAccessRequest)
            {
                QuotedMessageId = opened.QuotedMessageId,
                QuotedPreview = opened.QuotedPreview
            });
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
            var opened = OpenGroupCopy(identity, members, otherPartyUserId, message);
            conversation.Add(new ReadableChatMessage(
                isMine,
                opened.Text,
                message.SentAtUtc,
                message.IsEdited,
                IsWaitingToSend: false,
                SenderName: isMine ? _translations["You"] : NameOf(members, message.SenderUserId),
                MessageId: message.Id,
                GroupMessageId: message.GroupMessageId,
                ForwardedFromDisplayName: opened.ForwardedFromDisplayName,
                IsReadByEveryone: isMine ? message.IsReadByEveryone : null,
                Invitation: opened.Invitation,
                EditAccessRequest: opened.EditAccessRequest)
            {
                QuotedMessageId = opened.QuotedMessageId,
                QuotedPreview = opened.QuotedPreview
            });
        }

        foreach (var message in queued)
        {
            var opened = Read(message.Text);
            conversation.Add(new ReadableChatMessage(
                IsMine: true, opened.Text, message.QueuedAtUtc, IsEdited: false, IsWaitingToSend: true,
                SenderName: _translations["You"],
                ForwardedFromDisplayName: opened.ForwardedFromDisplayName,
                Invitation: opened.Invitation,
                EditAccessRequest: opened.EditAccessRequest)
            {
                QuotedMessageId = opened.QuotedMessageId,
                QuotedPreview = opened.QuotedPreview
            });
        }

        return conversation;
    }

    /// <summary>
    /// One opened message: its words, and who wrote them first if it got here by being passed on.
    /// </summary>
    private readonly record struct OpenedMessage(
        string? Text, string? ForwardedFromDisplayName, SharedItemInvitation? Invitation = null,
        EditAccessRequest? EditAccessRequest = null, Guid? QuotedMessageId = null, string? QuotedPreview = null);

    /// <summary>
    /// Decrypts, then reads whatever came out. The two belong together: a forward's or a reply's words
    /// are inside a payload, so anything that decrypted but was not unwrapped would show the reader raw
    /// JSON.
    /// </summary>
    private static OpenedMessage Open(ChatIdentity identity, string otherPartyPublicKeyBase64, EncryptedText encrypted)
        => identity.Decrypt(otherPartyPublicKeyBase64, encrypted) is { } plainText
            ? Read(plainText)
            : new OpenedMessage(null, null);

    /// <summary>
    /// What one plaintext says. Split out from <see cref="Open"/> because a message still in the queue
    /// has never been encrypted - see EncryptedChatMessageSender - and needs reading all the same.
    /// </summary>
    private static OpenedMessage Read(string plainText)
    {
        if (ForwardedMessage.TryUnwrap(plainText) is { } forwarded)
        {
            return new OpenedMessage(forwarded.Content, forwarded.OriginalAuthorDisplayName);
        }

        // An answer to one particular message, which the screen quotes above the reply itself.
        if (ReplyMessage.TryUnwrap(plainText) is { } reply)
        {
            return new OpenedMessage(
                reply.Content, null, QuotedMessageId: reply.ReplyToMessageId, QuotedPreview: reply.ReplyToPreview);
        }

        // An offer to share something, which is an ordinary message whose plaintext is structured - see
        // SharedItemInvitation. Its text is left null so the screen shows the offer rather than the JSON.
        if (SharedItemInvitation.TryRead(plainText) is { } invitation)
        {
            return new OpenedMessage(null, null, invitation);
        }

        // Somebody asking to be allowed to change something of yours. Nothing to accept in one tap -
        // widening access means sharing it again - so it is shown as what it is: a sentence.
        return EditAccessRequest.TryRead(plainText) is { } request
            ? new OpenedMessage(null, null, null, request)
            : new OpenedMessage(plainText, null);
    }

    /// <summary>
    /// Nothing openable when the other party to this copy has no cached key - they left the group, or
    /// their account is gone - which the screen shows as one unopenable message rather than an empty
    /// conversation.
    /// </summary>
    private static OpenedMessage OpenGroupCopy(
        ChatIdentity identity, IReadOnlyList<LocalChatGroupMember> members, Guid otherPartyUserId, LocalChatMessage message)
        => FindMember(members, otherPartyUserId)?.PublicKeyBase64 is { } otherPartyPublicKey
            ? Open(identity, otherPartyPublicKey, new EncryptedText(message.CiphertextBase64, message.NonceBase64))
            : new OpenedMessage(null, null);

    private static string NameOf(IReadOnlyList<LocalChatGroupMember> members, Guid userId)
        => FindMember(members, userId)?.DisplayName ?? "Someone";

    private static LocalChatGroupMember? FindMember(IReadOnlyList<LocalChatGroupMember> members, Guid userId)
        => members.FirstOrDefault(member => member.UserId == userId);

    private async Task<Guid> RequireSignedInUserIdAsync()
        => await _sessionStore.GetAsync() is { } session
            ? session.UserId
            : throw new EncryptionKeyLockedException();
}
