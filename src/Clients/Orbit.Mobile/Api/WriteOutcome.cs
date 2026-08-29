namespace Orbit.Mobile.Api;

/// <summary>
/// What the server did with a write the phone had already applied locally. Shared by every entity type
/// on the sync spine, because the three answers that matter are the same for all of them.
/// </summary>
public enum WriteOutcome
{
    Applied,

    /// <summary>
    /// Somebody else held the edit lock. Under the offline policy this should be rare - shared items are
    /// not editable offline - but sharing can change while the phone is away, so it has to be handled
    /// rather than assumed impossible.
    /// </summary>
    Refused,

    /// <summary>It is gone server-side. Nothing queued against it can ever succeed.</summary>
    Gone
}
