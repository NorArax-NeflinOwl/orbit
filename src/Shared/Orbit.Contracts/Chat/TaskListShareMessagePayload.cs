namespace Orbit.Contracts.Chat;

/// <summary>
/// Structured payload sent as an otherwise-ordinary chat message's plaintext body when a task list is
/// shared with the recipient, so it travels through the same end-to-end encryption as any other
/// message. Mirrors EventShareMessagePayload - see its class comment for the reasoning behind this shape.
/// </summary>
public sealed record TaskListShareMessagePayload(Guid ShareId, string TaskListTitle)
{
    public const string MessageType = "orbit/task-list-share";

    public string Type { get; init; } = MessageType;
}
