namespace Orbit.Mobile.Sync;

/// <summary>How the last attempt to reach Orbit went, as one word for the corner of the screen.</summary>
public enum SyncCondition
{
    /// <summary>Nothing has tried yet since launch.</summary>
    Unknown,

    Syncing,

    /// <summary>Everything local is on the server and vice versa.</summary>
    Synced,

    /// <summary>The phone believes it has no connection. Not a fault - the app keeps working.</summary>
    Offline,

    /// <summary>Reachable, and the attempt failed anyway. The one condition worth a second look.</summary>
    Failed
}

/// <summary>
/// Whether the app is in step with the server, shared by every screen.
///
/// Each section used to say this for itself at the top of its own page, which meant the answer depended
/// on which page you happened to be looking at - and said nothing at all on a page with no sync of its
/// own. One state, reported by whoever last synchronised, is both truer and the only shape that suits a
/// single indicator in the corner.
/// </summary>
public sealed class SyncState
{
    private readonly INetworkStatus _networkStatus;
    private readonly TimeProvider _timeProvider;

    public SyncState(INetworkStatus networkStatus, TimeProvider timeProvider)
    {
        _networkStatus = networkStatus;
        _timeProvider = timeProvider;
    }

    public event EventHandler? Changed;

    public SyncCondition Condition { get; private set; } = SyncCondition.Unknown;

    /// <summary>When the last attempt actually succeeded, or null if none has since launch.</summary>
    public DateTimeOffset? LastSyncedAtUtc { get; private set; }

    public void RecordStarted() => MoveTo(SyncCondition.Syncing);

    public void RecordSucceeded()
    {
        LastSyncedAtUtc = _timeProvider.GetUtcNow();
        MoveTo(SyncCondition.Synced);
    }

    /// <summary>
    /// Being offline and being refused are different things and the indicator says so: one is the app
    /// working as designed, the other is worth looking at. The distinction comes from the phone's own
    /// belief about connectivity rather than from the failure, because a request that never left has no
    /// status code to read.
    /// </summary>
    public void RecordFailed()
        => MoveTo(_networkStatus.IsOnline ? SyncCondition.Failed : SyncCondition.Offline);

    private void MoveTo(SyncCondition condition)
    {
        if (Condition == condition)
        {
            return;
        }

        Condition = condition;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
