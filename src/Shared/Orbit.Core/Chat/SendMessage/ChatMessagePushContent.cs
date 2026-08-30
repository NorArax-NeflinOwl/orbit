using Orbit.Core.Notifications;

namespace Orbit.Core.Chat.SendMessage;

/// <summary>
/// Builds the push notification payload for a newly received chat message. Deliberately never includes
/// the message itself: Orbit.Api only ever stores and relays ciphertext (see <see cref="ChatMessage"/>'s
/// class comment), so the server has no plaintext to put in a notification even if it wanted to.
/// </summary>
public static class ChatMessagePushContent
{
    public static PushNotificationPayload Build(Guid senderUserId, string senderDisplayName)
        => new("New message", "New message from {0}", [senderDisplayName], $"/chat/{senderUserId}");
}
