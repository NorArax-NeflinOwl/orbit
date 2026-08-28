namespace Orbit.Mobile.Data;

/// <summary>What a queued change does.</summary>
public enum OutboxOperation
{
    Create,
    Update,
    Delete
}

/// <summary>
/// One local change waiting to reach the server, replayed in the order it was made - see
/// info/orbit-maui-plan.md §5.4.
///
/// Order is the whole point of persisting these rather than just marking rows dirty: creating something
/// offline and then editing it twice has to arrive as a create followed by those edits, not as a single
/// "this changed" that has lost how it got that way.
///
/// One queue for every kind of thing rather than one per feature, and for the same reason the server
/// keeps one tombstone table (see <see cref="Orbit.Core.Sync.SyncEntityType"/>): the ordering that
/// matters is the order the user did things in, which a queue per feature cannot express.
/// </summary>
public sealed class OutboxEntry
{
    public long Id { get; set; }

    /// <summary>Which kind of thing this change is about - one of <see cref="Orbit.Core.Sync.SyncEntityType"/>.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// What this change is about, by its local id. Deliberately not the server id: a change queued
    /// against something the server has never seen has no server id to name it by yet, and it is
    /// resolved at send time instead.
    /// </summary>
    public Guid LocalId { get; set; }

    /// <summary>
    /// The server id this change targets, captured when a delete is queued. A delete outlives the local
    /// row it is about - the row leaves the phone the moment the user deletes it - so unless the id is
    /// kept here there is nothing left to tell the server which thing to remove. Null for creates and
    /// updates, which resolve it from the row itself at send time.
    /// </summary>
    public Guid? ServerId { get; set; }

    public OutboxOperation Operation { get; set; }

    public DateTimeOffset QueuedAtUtc { get; set; }

    /// <summary>
    /// How many times sending this has failed. Kept so a change the server will never accept can be
    /// given up on rather than blocking everything queued behind it forever.
    /// </summary>
    public int FailedAttempts { get; set; }
}
