using System.Text.Json;
using System.Text.Json.Serialization;
using Orbit.Contracts.Chat;

namespace Orbit.Mobile.Chat;

/// <summary>
/// Source-generated serialization for the structured payloads that travel as a message's plaintext.
/// Release builds trim and AOT-compile, which strips the reflection System.Text.Json would otherwise
/// need - the same reason the local store has one of these.
/// </summary>
[JsonSerializable(typeof(ForwardedMessagePayload))]
internal sealed partial class ChatPayloadSerializerContext : JsonSerializerContext;

/// <summary>
/// Passing a message on to somebody else. The server never learns that a forward happened: it holds
/// ciphertext either way, so the fact travels inside the plaintext as a payload the recipient unwraps -
/// see <see cref="ForwardedMessagePayload"/>.
///
/// Forwarding is a re-encryption, not a re-send. The original ciphertext is sealed between two specific
/// people and means nothing to a third, so what moves is the text, sealed again for wherever it is
/// going.
/// </summary>
public static class ForwardedMessage
{
    /// <summary>
    /// What to send. Your own message goes as ordinary text: forwarding something you wrote is
    /// indistinguishable from typing it again, so wrapping it would only cost the recipient an unwrap
    /// and claim an attribution that says nothing.
    /// </summary>
    /// <param name="originalAuthorDisplayName">
    /// Whoever wrote it first. For a message that is already a forward this is the name it arrived
    /// with, not the person who passed it on - attribution stays with the author rather than walking
    /// along the chain.
    /// </param>
    public static string Wrap(bool isMine, Guid originalAuthorUserId, string originalAuthorDisplayName, string content)
        => isMine
            ? content
            : JsonSerializer.Serialize(
                new ForwardedMessagePayload(originalAuthorUserId, originalAuthorDisplayName, content),
                ChatPayloadSerializerContext.Default.ForwardedMessagePayload);

    /// <summary>
    /// The forward inside this plaintext, or null when it is ordinary text. Text that happens to be JSON
    /// but does not carry the right marker is ordinary text too - a message reading "{}" is a message
    /// reading "{}", not a broken payload.
    /// </summary>
    public static ForwardedMessagePayload? TryUnwrap(string plainText)
    {
        if (plainText.Length == 0 || plainText[0] != '{')
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize(plainText, ChatPayloadSerializerContext.Default.ForwardedMessagePayload);
            return payload is { Type: ForwardedMessagePayload.MessageType } ? payload : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
