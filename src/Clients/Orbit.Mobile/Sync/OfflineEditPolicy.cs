namespace Orbit.Mobile.Sync;

/// <summary>
/// The two facts that decide whether something may be changed offline: whether it reached this user
/// through somebody else's share, and whether they shared it out to somebody else. Every syncable thing
/// that can be shared answers both, which is why the policy takes this rather than one entity type.
/// </summary>
public interface ISharedState
{
    bool IsShared { get; }

    bool IsSharedWithOthers { get; }
}

/// <summary>Why a note cannot be changed right now, or <see cref="None"/> when it can.</summary>
public enum OfflineEditRefusal
{
    None,

    /// <summary>Somebody shared this note with the user; its owner may be editing it.</summary>
    SharedWithYou,

    /// <summary>The user owns it but shared it out; whoever they shared it with may be editing it.</summary>
    SharedWithOthers
}

/// <summary>
/// The restrictive conflict policy from info/orbit-maui-plan.md §5.4: offline, a note may only be
/// changed if nobody else can change it.
///
/// The reason is Orbit's own design. Shared notes are protected by server-held, time-limited edit
/// locks, and sharing is not a copy - two people with CanEdit are editing one row. An offline client
/// cannot hold a lock, so it can only discover at replay time that someone else was editing, by which
/// point the user has already done the work. Refusing up front is honest and surprises nobody; the
/// alternative delivers "your change was rejected" long after the fact and needs a conflict UI to do it.
///
/// Online, this says nothing - the server's locks are the authority there, and they are better at it.
/// </summary>
public static class OfflineEditPolicy
{
    public static OfflineEditRefusal Evaluate(ISharedState item, INetworkStatus networkStatus)
    {
        if (networkStatus.IsOnline)
        {
            return OfflineEditRefusal.None;
        }

        if (item.IsShared)
        {
            return OfflineEditRefusal.SharedWithYou;
        }

        return item.IsSharedWithOthers ? OfflineEditRefusal.SharedWithOthers : OfflineEditRefusal.None;
    }

    public static bool IsAllowed(ISharedState item, INetworkStatus networkStatus)
        => Evaluate(item, networkStatus) is OfflineEditRefusal.None;
}
