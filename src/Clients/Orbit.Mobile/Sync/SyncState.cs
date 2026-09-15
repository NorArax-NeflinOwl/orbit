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

    /// <summary>
    /// The deployment said it is stopped on purpose - see <see cref="ServerReachability"/>. Not a fault
    /// either, and not "no connection": the phone has one, and the app keeps working as it does without.
    /// </summary>
    Paused,

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
    private readonly ServerReachability _reachability;
    private readonly TimeProvider _timeProvider;

    public SyncState(ServerReachability reachability, TimeProvider timeProvider)
    {
        _reachability = reachability;
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
    /// Being offline, being paused and being refused are three different things and the indicator says
    /// so: the first two are the app working as designed, the third is worth looking at. The
    /// distinction comes from what the phone believes about its network and about the deployment rather
    /// than from the failure, because a request that never left has no status code to read - and one
    /// answered by the platform in Orbit's place has had its status taken away on purpose, see
    /// <see cref="AnswerNotFromOrbitException"/>.
    /// </summary>
    public void RecordFailed()
    {
        if (_reachability.IsPaused)
        {
            MoveTo(SyncCondition.Paused);
            return;
        }

        MoveTo(_reachability.HasNetwork ? SyncCondition.Failed : SyncCondition.Offline);
    }

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
