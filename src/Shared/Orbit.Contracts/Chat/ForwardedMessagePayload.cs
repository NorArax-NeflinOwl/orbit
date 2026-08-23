namespace Orbit.Contracts.Chat;

/// <summary>
/// Structured payload sent as an otherwise-ordinary chat message's plaintext body when a message is
/// forwarded from one chat into another, so it travels through the same end-to-end encryption as any
/// other message. Mirrors EventShareMessagePayload - see its class comment for the reasoning behind this
/// shape: the server only ever sees ciphertext, so it has no way to know (or need to know) that a
/// message is a forward at all.
///
/// Only used when the forwarded message wasn't the forwarder's own - forwarding your own message is
/// indistinguishable from typing the same text again, so Chat.razor sends that case as an ordinary,
/// unwrapped message instead of paying for a JSON payload it would have to unwrap on the other end.
/// <see cref="OriginalAuthorUserId"/> is the identifier this shape exists to carry: it's what lets the
/// recipient's Chat.razor render "Forwarded from {OriginalAuthorDisplayName}" instead of attributing the
/// message to whoever actually forwarded it.
/// </summary>
public sealed record ForwardedMessagePayload(Guid OriginalAuthorUserId, string OriginalAuthorDisplayName, string Content)
{
    public const string MessageType = "orbit/forwarded-message";

    public string Type { get; init; } = MessageType;
}
