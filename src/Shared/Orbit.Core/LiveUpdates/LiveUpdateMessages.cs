namespace Orbit.Core.LiveUpdates;

/// <summary>
/// The names the server announces under and the client listens for. Here rather than spelled out on
/// each side because they are one agreement, not two: a name changed in one place and not the other
/// produces no error anywhere - the server sends into the void, the client waits forever, and the only
/// symptom is that the app quietly goes back to being as slow as it was before.
/// </summary>
public static class LiveUpdateMessages
{
    /// <summary>Where the connection lives. Under /api so it reaches the server by the route everything else already takes.</summary>
    public const string Path = "/api/live";

    public const string ChatChanged = "ChatChanged";
    public const string NotificationsChanged = "NotificationsChanged";

    /// <summary>Carries whose presence changed, so a client can refresh one row rather than the roster.</summary>
    public const string PresenceChanged = "PresenceChanged";

    /// <summary>
    /// What the client calls to say somebody is still at the keyboard. A method rather than something
    /// the server infers from the connection being open: a tab left behind thirty others keeps its
    /// connection perfectly alive, and presence is meant to say whether somebody is there to answer.
    /// See PresenceService, which has always followed that rule and still does.
    /// </summary>
    public const string ReportPresence = "ReportPresenceAsync";
}
