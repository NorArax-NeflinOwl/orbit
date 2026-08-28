namespace Orbit.Mobile.Data;

/// <summary>What a local write did.</summary>
public enum LocalWriteOutcome
{
    Applied,

    /// <summary>It is not there - deleted underneath the caller.</summary>
    NotFound,

    /// <summary>
    /// It reached this user through somebody else's share, and what was asked for is the owner's alone -
    /// pinning, today. Distinct from <see cref="RefusedWhileOffline"/>: a connection would not help, and
    /// distinct from <see cref="NotFound"/>, which would send a screen looking for a row that is there.
    /// </summary>
    NotYours,

    /// <summary>
    /// Refused because somebody else can change this and there is no connection to take an edit lock
    /// with - see OfflineEditPolicy. Refused here rather than only hidden on screen: a screen that forgot
    /// to check would otherwise queue an edit the policy exists to prevent, and the queue is where the
    /// damage would be done.
    /// </summary>
    RefusedWhileOffline
}
