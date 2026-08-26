namespace Orbit.Mobile.Data;

/// <summary>
/// How far this device has caught up for one kind of entity. The value is whatever the server's change
/// feed handed back last time - an ISO-8601 UTC string, opaque here on purpose, so the client never has
/// to agree with the server about what a cursor means.
///
/// One row per entity type rather than one global cursor: the feeds move independently, and a failure
/// pulling calendar events must not rewind notes.
/// </summary>
public sealed class SyncCursor
{
    public string EntityType { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
