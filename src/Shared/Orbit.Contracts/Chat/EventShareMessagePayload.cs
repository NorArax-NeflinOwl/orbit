namespace Orbit.Contracts.Chat;

/// <summary>
/// Structured payload sent as an otherwise-ordinary chat message's plaintext body when a calendar event
/// is shared with the recipient, so it travels through the same end-to-end encryption as any other
/// message. Chat.razor tries to deserialize every decrypted message as this shape and, when Type matches
/// MessageType, renders an "Akceptuj" action instead of plain text (see CalendarApiClient's share/accept
/// endpoints) rather than the ciphertext itself carrying anything the server needs to know - the
/// CalendarEventShare row created via ShareCalendarEventCommand is what the server actually relies on.
/// </summary>
public sealed record EventShareMessagePayload(Guid ShareId, string EventTitle)
{
    public const string MessageType = "orbit/event-share";

    public string Type { get; init; } = MessageType;
}
