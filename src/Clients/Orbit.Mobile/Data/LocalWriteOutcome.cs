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
    RefusedWhileOffline,

    /// <summary>
    /// Refused because this arrived through a share that does not permit editing - see SharedItemAccess.
    /// Unlike the one above it, this holds online as well: it is not about locks but about what the
    /// owner allowed, and the server answers such a write with 403 whatever the connection is like.
    /// </summary>
    RefusedAsReadOnly
}

/// <summary>What a screen does with an outcome, said once rather than at each of the eight call sites.</summary>
public static class LocalWrites
{
    public static bool WasRefused(this LocalWriteOutcome outcome)
        => outcome is LocalWriteOutcome.RefusedWhileOffline or LocalWriteOutcome.RefusedAsReadOnly;

    /// <summary>
    /// Which of the two refusals it was, in words. The offline one is worded per screen - "this note",
    /// "this list" - which is why the caller hands its own in; a read-only share reads the same
    /// everywhere, because what it says is about the share rather than about the thing.
    /// </summary>
    public static string Explain(
        this LocalWriteOutcome outcome, string offlineExplanation, Orbit.Mobile.Localization.Translations translations)
        => outcome is LocalWriteOutcome.RefusedAsReadOnly
            ? translations["Shared with you to read. Ask whoever shared it if you need to change it."]
            : translations[offlineExplanation];
}
