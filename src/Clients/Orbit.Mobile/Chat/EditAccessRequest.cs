using System.Text.Json;
using Orbit.Contracts.Chat;

namespace Orbit.Mobile.Chat;

/// <summary>
/// Asking whoever owns something to let you change it. Sent as an ordinary chat message, like the share
/// offers, so it travels through the same end-to-end encryption and the server learns nothing about it.
///
/// Deliberately a request rather than a grant: only the owner can widen access, so all this does is ask.
/// They answer by sharing it again at a level that permits editing.
/// </summary>
public sealed record EditAccessRequest(SharedItemKind Kind, Guid ItemId, string Name)
{
    /// <summary>The message that asks, which is the same shape Orbit.Web sends.</summary>
    public string ToMessage()
        => JsonSerializer.Serialize(
            new EditAccessRequestPayload(Kind.ToString(), ItemId, Name),
            ChatPayloadSerializerContext.Default.EditAccessRequestPayload);

    /// <inheritdoc cref="SharedItemInvitation.TryRead"/>
    public static EditAccessRequest? TryRead(string plainText)
    {
        if (plainText.Length == 0 || plainText[0] != '{')
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(plainText, ChatPayloadSerializerContext.Default.EditAccessRequestPayload)
                is { Type: EditAccessRequestPayload.MessageType } payload
                && Enum.TryParse<SharedItemKind>(payload.ItemType, out var kind)
                ? new EditAccessRequest(kind, payload.ItemId, payload.ItemTitle)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
