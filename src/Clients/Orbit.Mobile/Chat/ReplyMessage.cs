using System.Text.Json;
using Orbit.Contracts.Chat;

namespace Orbit.Mobile.Chat;

/// <summary>
/// Answering one particular message rather than the conversation in general. Like a forward, the server
/// never learns that it happened: it holds ciphertext either way, so the fact travels inside the
/// plaintext as a payload the recipient unwraps - see <see cref="ReplyMessagePayload"/>.
///
/// The quote is carried rather than looked up, which is the payload's own reasoning: the message being
/// answered may have been edited or deleted since, and a quote of something no longer there is still
/// what the reply was answering.
/// </summary>
public static class ReplyMessage
{
    public static string Wrap(Guid replyToMessageId, string replyToContent, string content)
        => JsonSerializer.Serialize(
            new ReplyMessagePayload(replyToMessageId, ReplyMessagePayload.Preview(replyToContent), content),
            ChatPayloadSerializerContext.Default.ReplyMessagePayload);

    /// <inheritdoc cref="ForwardedMessage.TryUnwrap"/>
    public static ReplyMessagePayload? TryUnwrap(string plainText)
    {
        if (plainText.Length == 0 || plainText[0] != '{')
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize(plainText, ChatPayloadSerializerContext.Default.ReplyMessagePayload);
            return payload is { Type: ReplyMessagePayload.MessageType } ? payload : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
