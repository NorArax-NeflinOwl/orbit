namespace Orbit.Core.Sync;

/// <summary>
/// A record that something was deleted, kept because deletion is the one change a delta cannot express.
/// A client asking "what changed since X" sees updated rows by their timestamp, but a deleted row simply
/// isn't there - indistinguishable from one it already knows about - so without this a note deleted on
/// the web would live on a phone forever.
///
/// One table for every kind of thing rather than a soft-delete flag on each: a flag means every existing
/// query has to remember to exclude deleted rows, and the one that forgets resurrects data. Deletes stay
/// real deletes; this just leaves a note saying one happened.
/// </summary>
/// <param name="EntityType">Which kind of thing was deleted - see <see cref="SyncEntityType"/>.</param>
public sealed record SyncTombstone(Guid UserId, string EntityType, Guid EntityId, DateTimeOffset DeletedAtUtc);

/// <summary>
/// The kinds of thing a tombstone can describe. Constants rather than an enum because the value is
/// stored as text and travels to clients in JSON, where a renamed enum member would silently change the
/// wire format.
/// </summary>
public static class SyncEntityType
{
    public const string Note = "Note";
    public const string TaskList = "TaskList";
    public const string CalendarEvent = "CalendarEvent";
    public const string Inventory = "Inventory";

    /// <summary>
    /// One entry in the in-app notification feed. Unlike the four above, nothing ever writes a tombstone
    /// for one - an entry leaves only by outliving its retention window, which a client works out from
    /// the age of what it holds. The constant exists so the feed can use the same delta shape.
    /// </summary>
    public const string NotificationEntry = "NotificationEntry";
}
