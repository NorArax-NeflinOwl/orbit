namespace Orbit.Contracts.Chat;

/// <summary>
/// Structured payload sent as an otherwise-ordinary chat message's plaintext body when someone shares
/// their position, so it travels through the same end-to-end encryption as any other message.
///
/// Unlike the four share payloads it sits beside, this one carries no ShareId and needs no accepting: a
/// position is simply visible to the person it was sealed for. What it carries instead is whether the
/// share is a single point or a live one, because those are two quite different things to be told.
/// </summary>
public sealed record LocationShareMessagePayload(bool IsContinuous)
{
    public const string MessageType = "orbit/location-share";

    public string Type { get; init; } = MessageType;
}
