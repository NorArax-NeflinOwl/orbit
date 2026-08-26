namespace Orbit.Mobile.Data;

/// <summary>What a local write did.</summary>
public enum LocalWriteOutcome
{
    Applied,

    /// <summary>It is not there - deleted underneath the caller.</summary>
    NotFound,

    /// <summary>
    /// Refused because somebody else can change this and there is no connection to take an edit lock
    /// with - see OfflineEditPolicy. Refused here rather than only hidden on screen: a screen that forgot
    /// to check would otherwise queue an edit the policy exists to prevent, and the queue is where the
    /// damage would be done.
    /// </summary>
    RefusedWhileOffline
}
