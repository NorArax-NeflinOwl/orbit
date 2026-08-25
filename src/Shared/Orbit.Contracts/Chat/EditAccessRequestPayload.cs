namespace Orbit.Contracts.Chat;

/// <summary>
/// Structured payload sent as an otherwise-ordinary chat message when someone holding read-only access
/// asks the owner to let them edit. It travels through the same end-to-end encryption as any other
/// message, and like the share payloads it carries nothing the server needs to know - the server learns
/// what happened only if the owner agrees, at which point an ordinary share request raises the level.
///
/// Deliberately a request rather than a grant: only the owner can widen access, so all this does is ask.
/// </summary>
/// <param name="ItemType">One of "Note", "TaskList", "CalendarEvent", "Warehouse".</param>
public sealed record EditAccessRequestPayload(string ItemType, Guid ItemId, string ItemTitle)
{
    public const string MessageType = "orbit/edit-access-request";

    public string Type { get; init; } = MessageType;
}
