namespace Orbit.Mobile.Notifications;

/// <summary>
/// Which screen a notification leads to. The server records a web path with every notification - it
/// was written for the browser, where the path *is* the destination - and a phone has no such thing,
/// so the path has to be read rather than followed.
///
/// The set of paths is closed and known: every one is produced by a PushContent type in Orbit.Core
/// (ChatMessagePushContent, EventReminderPushContent, and the rest). Anything outside that set means
/// the server learned to send somewhere this build does not know about, which is a thing to survive
/// rather than a thing to crash on - see <see cref="Parse"/>.
/// </summary>
public enum NotificationTarget
{
    /// <summary>A one-to-one conversation, identified by the other person's user id.</summary>
    Conversation,

    /// <summary>A group conversation, identified by the group id.</summary>
    GroupConversation,

    /// <summary>One task list, identified by its server id.</summary>
    TaskList,

    /// <summary>The calendar. The path names an event, but there is no per-event screen to land on.</summary>
    Calendar,

    Inventory,

    Map,

    /// <summary>
    /// The copies taken offline that are waiting to be decided on. The only destination the phone
    /// raises for itself: a copy has no server id to name, and nobody but this device knows it exists.
    /// </summary>
    CopyReview
}

/// <summary>
/// A parsed notification path: where to go, and which thing to open when the destination names one.
/// </summary>
/// <param name="Id">
/// The server id in the path, or null for a destination that names no particular thing. Kept nullable
/// rather than defaulting to <see cref="Guid.Empty"/> so "this destination has no id" cannot be
/// mistaken for "this destination's id is all zeroes".
/// </param>
public sealed record NotificationDestination(NotificationTarget Target, Guid? Id = null)
{
    /// <summary>
    /// Reads one of the server's notification paths. Returns null for anything unrecognised - an
    /// unknown path is not an error worth surfacing: the entry still lists and still reads, it simply
    /// cannot be tapped through, which is a far better outcome on an older build than a crash.
    /// </summary>
    public static NotificationDestination? Parse(string? url)
    {
        var segments = (url ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return segments switch
        {
            ["chat", "groups", var groupId] => ForId(NotificationTarget.GroupConversation, groupId),
            ["chat", var userId] => ForId(NotificationTarget.Conversation, userId),
            ["tasks", var taskListId] => ForId(NotificationTarget.TaskList, taskListId),
            // The path names the event, but the app has no screen for one event on its own, so the id
            // is deliberately dropped rather than carried to somewhere that cannot use it.
            ["calendar", _] or ["calendar"] => new NotificationDestination(NotificationTarget.Calendar),
            ["inventory"] => new NotificationDestination(NotificationTarget.Inventory),
            ["copies"] => new NotificationDestination(NotificationTarget.CopyReview),
            // The id names which copy the notice is about, so answering that one can take its notice
            // away again. The window itself shows them all, so the opener has no use for it.
            ["copies", var copyLocalId] => ForId(NotificationTarget.CopyReview, copyLocalId),
            ["map"] => new NotificationDestination(NotificationTarget.Map),
            _ => null
        };
    }

    private static NotificationDestination? ForId(NotificationTarget target, string id)
        => Guid.TryParse(id, out var parsed) ? new NotificationDestination(target, parsed) : null;
}
