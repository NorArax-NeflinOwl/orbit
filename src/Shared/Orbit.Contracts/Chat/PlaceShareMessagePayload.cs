namespace Orbit.Contracts.Chat;

/// <summary>
/// What a chat message says when somebody hands a place over, sent as that message's plaintext body so
/// it travels through the same end-to-end encryption as anything else said in the conversation. Mirrors
/// InventoryShareMessagePayload - see EventShareMessagePayload for the reasoning behind this shape.
/// </summary>
public sealed record PlaceShareMessagePayload(Guid ShareId, string PlaceName)
{
    public const string MessageType = "orbit/place-share";

    public string Type { get; init; } = MessageType;
}
