namespace Orbit.Mobile.Data;

/// <summary>
/// One entry of the in-app notification feed, as this phone holds it.
///
/// The feed was the last thing in the app with no local copy: every other screen reads from the local
/// store and syncs, this one asked the server each time, so with no connection it was simply empty -
/// and marking one read or clearing them failed. Whatever the phone was told about is now kept here,
/// which is also what lets an overdue task still be visible on a train.
///
/// Pulled and never pushed: a notification the server wrote is the server's, and there is no outbox
/// entry for one.
///
/// <b>The phone raises a few of its own</b> - see <see cref="IsRaisedHere"/>. They are the things only
/// this device knows: a queued change the server would not take, and a copy made offline that is waiting
/// to be decided on. Both are facts about this phone, so there is nobody to have been told them by.
/// </summary>
public sealed class LocalNotification
{
    public Guid Id { get; set; }

    /// <summary>NotificationEntryKind by name, as the wire carries it.</summary>
    public string Kind { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    /// <summary>Where tapping it goes, or null when it points nowhere.</summary>
    public string? Url { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public bool IsRead { get; set; }

    /// <summary>Cleared out of the panel, but still on the notifications page - see the server's own rule.</summary>
    public bool IsDismissed { get; set; }

    /// <summary>
    /// The pieces Orbit writes a title and a body from, so each client can say them in its own language
    /// - see OrbitWrittenNames. Stored as they arrived; the screen is what turns them into words.
    /// </summary>
    public string TitleArgumentsJson { get; set; } = "[]";

    /// <inheritdoc cref="TitleArgumentsJson"/>
    public string BodyArgumentsJson { get; set; } = "[]";

    /// <summary>
    /// Written by this phone rather than pulled from the server, and so unknown to it: marking one read
    /// or clearing it is a local matter, and no pull will ever bring it back or take it away.
    /// </summary>
    public bool IsRaisedHere { get; set; }
}
