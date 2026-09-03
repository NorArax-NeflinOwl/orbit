using Orbit.Contracts.Chat;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Crypto;

namespace Orbit.Mobile.Chat;

/// <summary>
/// Gives somebody who joined a group late the conversation they arrived after - the phone's half of
/// Orbit.Web's GroupHistorySharing, against the same endpoint.
///
/// This has to happen on a device, and specifically on the device of a member who can already read the
/// group: every group message is sealed under a pairwise key (see Orbit.Core.Chat.ChatMessage.
/// CreateForGroup), the server holds none of them, and no copy of anything was ever made for the
/// newcomer. So the only way to give them the past is for somebody who has it to open it and seal it
/// again under the key they share with the new member.
///
/// What cannot be opened is left behind rather than passed on as ciphertext nobody can read.
/// </summary>
public sealed class GroupHistorySharing
{
    private readonly ChatClient _chatClient;
    private readonly OwnEncryptionKeyProvider _encryptionKeyProvider;
    private readonly ChatDirectoryReader _directoryReader;
    private readonly SessionStore _sessionStore;

    public GroupHistorySharing(
        ChatClient chatClient, OwnEncryptionKeyProvider encryptionKeyProvider,
        ChatDirectoryReader directoryReader, SessionStore sessionStore)
    {
        _chatClient = chatClient;
        _encryptionKeyProvider = encryptionKeyProvider;
        _directoryReader = directoryReader;
        _sessionStore = sessionStore;
    }

    /// <summary>
    /// How many earlier messages the recipient can now read that they could not before. Fewer than the
    /// conversation holds is an ordinary answer rather than a failure: a message sealed under a key pair
    /// this device has since replaced cannot be opened here either, and one nobody can read is not
    /// something to pass on.
    /// </summary>
    /// <exception cref="EncryptionKeyLockedException">This device holds no key, so it can open nothing.</exception>
    /// <exception cref="InvalidOperationException">
    /// The recipient has never signed in, so there is no key to seal anything for. Said rather than
    /// counted as nothing shared, because it is a wait rather than a dead end - see the group screen.
    /// </exception>
    public async Task<int> ShareWithAsync(
        Guid groupId, Guid recipientUserId, CancellationToken cancellationToken = default)
    {
        var ownUserId = await RequireOwnUserIdAsync();
        var conversation = await _chatClient.GetGroupConversationAsync(groupId, cancellationToken);

        // Everyone whose key is needed, asked for once: each copy is between its sender and its
        // recipient, so opening it needs the key of whichever of the two is not the reader.
        var parties = conversation
            .Select(message => OtherPartyOf(message, ownUserId))
            .Append(recipientUserId)
            .ToHashSet();

        var directory = await _directoryReader.ReadAsync(parties, [], cancellationToken);
        if (directory.FindPublicKey(recipientUserId) is not { } recipientPublicKey)
        {
            throw new InvalidOperationException(
                $"User {recipientUserId} has no public key on file yet - they must sign in at least once "
                + "before a group's history can be shared with them.");
        }

        using var identity = await _encryptionKeyProvider.OpenAsync(cancellationToken);
        var copies = Reseal(identity, directory, conversation, ownUserId, recipientPublicKey);

        return copies.Count == 0
            ? 0
            : await _chatClient.ShareGroupHistoryAsync(groupId, recipientUserId, copies, cancellationToken);
    }

    private static List<SharedHistoryCopyDto> Reseal(
        ChatIdentity identity, ChatDirectory directory, IReadOnlyList<ChatMessageDto> conversation,
        Guid ownUserId, string recipientPublicKey)
    {
        var copies = new List<SharedHistoryCopyDto>(conversation.Count);
        foreach (var message in conversation)
        {
            if (message.GroupMessageId is not { } groupMessageId
                || directory.FindPublicKey(OtherPartyOf(message, ownUserId)) is not { } otherPartyPublicKey)
            {
                continue;
            }

            var plainText = identity.Decrypt(
                otherPartyPublicKey, new EncryptedText(message.CiphertextBase64, message.NonceBase64));

            if (plainText is null)
            {
                continue;
            }

            var resealed = identity.Encrypt(recipientPublicKey, plainText);
            copies.Add(new SharedHistoryCopyDto(groupMessageId, resealed.CiphertextBase64, resealed.NonceBase64));
        }

        return copies;
    }

    /// <summary>Whichever end of this copy is not the reader - the key it was sealed under.</summary>
    private static Guid OtherPartyOf(ChatMessageDto message, Guid ownUserId)
        => message.SenderUserId == ownUserId ? message.RecipientUserId : message.SenderUserId;

    private async Task<Guid> RequireOwnUserIdAsync()
        => await _sessionStore.GetAsync() is { } session
            ? session.UserId
            : throw new EncryptionKeyLockedException();
}
