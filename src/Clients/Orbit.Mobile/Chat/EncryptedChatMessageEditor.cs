using Microsoft.Extensions.Logging;
using Orbit.Contracts.Chat;
using Orbit.Mobile.Api;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Chat;

/// <summary>What happened to a message somebody tried to change.</summary>
public enum ChatEditOutcome
{
    Done,

    /// <summary>Gone already, or never this account's to change - the screen reloads rather than insists.</summary>
    NotAllowed,

    /// <summary>Somebody in the group has no published key, so the fan-out cannot be completed.</summary>
    SomebodyHasNoChatKey,

    /// <summary>The server could not be reached. Unlike sending, this is not queued - see the class remarks.</summary>
    Offline
}

/// <summary>
/// Changing a message that has already been sent: rewriting it, or removing it for everyone.
///
/// <b>Both need a connection, and neither is queued.</b> That is a deliberate difference from sending,
/// which keeps what was typed and replays it later. An edit or a delete is an instruction about a
/// message the server already holds, and holding one on the phone would mean showing the reader a
/// history that nobody else has - the opposite of what "delete for everyone" is taken to mean. Sending
/// can be queued precisely because an unsent message exists nowhere else yet.
///
/// Editing a group message is the same fan-out as sending one, for the reason
/// <see cref="ChatDirectoryReader"/> gives: one copy per current member, sealed here and now.
/// </summary>
public sealed class EncryptedChatMessageEditor
{
    private readonly ChatRepository _chatRepository;
    private readonly ChatClient _chatClient;
    private readonly ChatDirectoryReader _directoryReader;
    private readonly OwnEncryptionKeyProvider _encryptionKeyProvider;
    private readonly ILogger<EncryptedChatMessageEditor> _logger;

    public EncryptedChatMessageEditor(
        ChatRepository chatRepository, ChatClient chatClient, ChatDirectoryReader directoryReader,
        OwnEncryptionKeyProvider encryptionKeyProvider, ILogger<EncryptedChatMessageEditor> logger)
    {
        _chatRepository = chatRepository;
        _chatClient = chatClient;
        _directoryReader = directoryReader;
        _encryptionKeyProvider = encryptionKeyProvider;
        _logger = logger;
    }

    /// <summary>
    /// Rewrites a one-to-one message. Re-encrypted rather than edited in place, because there is nothing
    /// to edit: the server holds ciphertext it cannot read, so a new text means a new sealing.
    /// </summary>
    public async Task<ChatEditOutcome> EditAsync(
        Guid messageId, Guid otherUserId, string text, CancellationToken cancellationToken = default)
    {
        using var identity = await _encryptionKeyProvider.OpenAsync(cancellationToken);

        try
        {
            var directory = await _directoryReader.ReadAsync([otherUserId], [], cancellationToken);
            if (directory.FindPublicKey(otherUserId) is not { } otherPublicKey)
            {
                return ChatEditOutcome.SomebodyHasNoChatKey;
            }

            var sealedText = identity.Encrypt(otherPublicKey, text);
            var edited = await _chatClient.EditMessageAsync(
                messageId, new EditMessageRequest(sealedText.CiphertextBase64, sealedText.NonceBase64), cancellationToken);

            if (!edited)
            {
                return ChatEditOutcome.NotAllowed;
            }

            await _chatRepository.RewriteAsync(messageId, sealedText, cancellationToken);
            return ChatEditOutcome.Done;
        }
        catch (Exception exception) when (IsOffline(exception, cancellationToken))
        {
            _logger.LogInformation("Could not reach the server to edit a message ({Reason})", exception.Message);
            return ChatEditOutcome.Offline;
        }
    }

    /// <inheritdoc cref="EditAsync"/>
    public async Task<ChatEditOutcome> EditGroupMessageAsync(
        Guid groupId, Guid groupMessageId, string text, CancellationToken cancellationToken = default)
    {
        using var identity = await _encryptionKeyProvider.OpenAsync(cancellationToken);

        try
        {
            var directory = await _directoryReader.ReadAsync([], [groupId], cancellationToken);
            if (directory.FindOtherMembers(groupId) is not { } otherMembers)
            {
                return ChatEditOutcome.NotAllowed;
            }

            var copies = new List<GroupMessageCopyDto>(otherMembers.Count);
            foreach (var memberUserId in otherMembers)
            {
                if (directory.FindPublicKey(memberUserId) is not { } memberPublicKey)
                {
                    return ChatEditOutcome.SomebodyHasNoChatKey;
                }

                var sealedForMember = identity.Encrypt(memberPublicKey, text);
                copies.Add(new GroupMessageCopyDto(
                    memberUserId, sealedForMember.CiphertextBase64, sealedForMember.NonceBase64));
            }

            var outcome = await _chatClient.EditGroupMessageAsync(groupId, groupMessageId, copies, cancellationToken);
            if (outcome is not GroupSendOutcome.Sent)
            {
                return ChatEditOutcome.NotAllowed;
            }

            // The re-sealed copies come back with new ids, so the old ones have to go or they would sit
            // alongside them showing the words that were just replaced.
            await _chatRepository.ForgetGroupMessageAsync(groupMessageId, cancellationToken);
            return ChatEditOutcome.Done;
        }
        catch (Exception exception) when (IsOffline(exception, cancellationToken))
        {
            _logger.LogInformation("Could not reach the server to edit a group message ({Reason})", exception.Message);
            return ChatEditOutcome.Offline;
        }
    }

    /// <summary>
    /// Removes a message for everyone. One copy's id is enough for a group message - the server takes
    /// every copy of the same posting with it.
    /// </summary>
    public async Task<ChatEditOutcome> DeleteAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await _chatClient.DeleteMessageAsync(messageId, cancellationToken))
            {
                return ChatEditOutcome.NotAllowed;
            }

            // Removed here too rather than waiting for a pull: the group conversation is pulled whole and
            // a one-to-one pull only asks for what is newer, so nothing would ever report the absence.
            await _chatRepository.ForgetAsync(messageId, cancellationToken);
            return ChatEditOutcome.Done;
        }
        catch (Exception exception) when (IsOffline(exception, cancellationToken))
        {
            _logger.LogInformation("Could not reach the server to delete a message ({Reason})", exception.Message);
            return ChatEditOutcome.Offline;
        }
    }

    private static bool IsOffline(Exception exception, CancellationToken cancellationToken)
        => !cancellationToken.IsCancellationRequested
            && exception is HttpRequestException or TaskCanceledException;
}
