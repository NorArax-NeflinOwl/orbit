using Orbit.Mobile.Data;

namespace Orbit.Mobile.Chat;

/// <summary>
/// Passes a message on into another conversation. Thin on purpose: the interesting part is deciding
/// what to send, which <see cref="ForwardedMessage"/> owns, and the sending is the ordinary queue - a
/// forward is a message like any other once its body has been worked out.
/// </summary>
public sealed class MessageForwarder
{
    private readonly EncryptedChatMessageSender _sender;

    public MessageForwarder(EncryptedChatMessageSender sender) => _sender = sender;

    /// <param name="authorDisplayName">
    /// Who to attribute it to when it was not the reader's own: the other party in a one-to-one
    /// conversation, or the member who wrote it in a group. Ignored for the reader's own message, which
    /// goes as ordinary text.
    /// </param>
    public Task<ChatSendResult> ForwardAsync(
        ReadableChatMessage message, Guid originalAuthorUserId, string authorDisplayName, LocalContact to,
        CancellationToken cancellationToken = default)
        => _sender.SendAsync(
            to.UserId,
            ForwardedMessage.Wrap(
                message.IsMine,
                originalAuthorUserId,
                // Already a forward: keep the name it arrived with, so passing something along a chain
                // still credits whoever wrote it rather than the last person to touch it.
                message.ForwardedFromDisplayName ?? authorDisplayName,
                message.Text ?? string.Empty),
            cancellationToken);
}
