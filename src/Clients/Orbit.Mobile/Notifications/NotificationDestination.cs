using Orbit.Mobile.Chat;
using CoreSharedItemKind = Orbit.Core.Notifications.SharedItemKind;

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
    CopyReview,

    /// <summary>
    /// A public link somebody was sent. Unlike every other destination here it names nothing in this
    /// account - what is behind it may belong to a stranger - and it arrives from the system rather
    /// than from a notification, when Android hands Orbit a link instead of the browser.
    /// </summary>
    SharedLink,

    /// <summary>
    /// Something offered to this reader and not yet taken up - see <see cref="InvitationOffer"/>. Like
    /// <see cref="SharedLink"/> it names nothing in this account yet, which is the whole point of it.
    /// </summary>
    Invitation
}

/// <summary>
/// What an invitation path names: which kind of thing was offered, which offer it is, and who made it.
///
/// The kind and the share's id are what the offer is read and accepted by; the sharer is who the screen
/// says it is from, and where it sends anybody who would rather answer in the conversation - which is
/// where this notification landed for as long as the phone had no screen of its own for an offer.
/// </summary>
public sealed record InvitationOffer(SharedItemKind Kind, Guid ShareId, Guid SharerUserId)
{
    /// <summary>
    /// The name this kind carries inside an address, which is what the offer is read and accepted by -
    /// see Orbit.Core.Notifications.SharedItemPath, whose names are stable.
    /// </summary>
    public string KindPath => Orbit.Core.Notifications.SharedItemPath.For(Kind switch
    {
        SharedItemKind.Note => CoreSharedItemKind.Note,
        SharedItemKind.TaskList => CoreSharedItemKind.TaskList,
        SharedItemKind.CalendarEvent => CoreSharedItemKind.CalendarEvent,
        SharedItemKind.Place => CoreSharedItemKind.Place,
        _ => CoreSharedItemKind.Inventory
    });
}

/// <summary>
/// A parsed notification path: where to go, and which thing to open when the destination names one.
/// </summary>
/// <param name="Id">
/// The server id in the path, or null for a destination that names no particular thing. Kept nullable
/// rather than defaulting to <see cref="Guid.Empty"/> so "this destination has no id" cannot be
/// mistaken for "this destination's id is all zeroes".
/// </param>
/// <param name="Token">
/// The secret out of a public link, which is not an id and not a Guid - see PublicShareLinkDto. Empty
/// for every other destination.
/// </param>
/// <param name="Offer">
/// The offer an invitation path names, and null for every other destination - three values rather than
/// the one <paramref name="Id"/> holds, so it is its own thing rather than a second id smuggled through
/// <paramref name="Token"/>.
/// </param>
public sealed record NotificationDestination(
    NotificationTarget Target, Guid? Id = null, string Token = "", InvitationOffer? Offer = null)
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
            // Something shared, waiting to be taken up. The app has its own screen for one since
            // 2026-09-10 - see InvitationViewModel - which is what the browser has always had.
            //
            // A kind this build does not know, and a position somebody shared rather than a thing
            // (SharedItemKind.Location, which nothing accepts), both fall back to what this path did
            // before that screen existed: the last segment is who offered it, and the conversation is
            // where this app's own Accept lives - see SharedItemAcceptance.
            ["invitation", var kind, var shareId, var sharerUserId]
                => ForOffer(kind, shareId, sharerUserId)
                    ?? ForId(NotificationTarget.Conversation, sharerUserId),
            ["tasks", var taskListId] => ForId(NotificationTarget.TaskList, taskListId),
            // The path names the event, but the app has no screen for one event on its own, so the id
            // is deliberately dropped rather than carried to somewhere that cannot use it.
            ["calendar", _] or ["calendar"] => new NotificationDestination(NotificationTarget.Calendar),
            ["inventory"] => new NotificationDestination(NotificationTarget.Inventory),
            // The path names the storage now (InventoryExpiryPushContent), and the phone opens one by
            // its *local* id rather than the server's - so the id is carried and the opener decides
            // whether it can be used, the same shape the task list already has.
            ["inventory", var inventoryId] => ForId(NotificationTarget.Inventory, inventoryId),
            ["copies"] => new NotificationDestination(NotificationTarget.CopyReview),
            // The id names which copy the notice is about, so answering that one can take its notice
            // away again. The window itself shows them all, so the opener has no use for it.
            ["copies", var copyLocalId] => ForId(NotificationTarget.CopyReview, copyLocalId),
            ["map"] => new NotificationDestination(NotificationTarget.Map),
            // The one path that is not the server's: it is what Orbit.Web serves a public link at, and
            // what a link handed to the app by Android carries - see MainActivity.
            ["s", var token] => new NotificationDestination(NotificationTarget.SharedLink, Token: token),
            _ => null
        };
    }

    private static NotificationDestination? ForId(NotificationTarget target, string id)
        => Guid.TryParse(id, out var parsed) ? new NotificationDestination(target, parsed) : null;

    /// <summary>
    /// The offer this path names, or null when it names one this app cannot show a screen for - the
    /// caller then falls back to the conversation, which is where the offer can still be accepted.
    /// </summary>
    private static NotificationDestination? ForOffer(string kind, string shareId, string sharerUserId)
        => KindOf(kind) is { } sharedItemKind
            && Guid.TryParse(shareId, out var share)
            && Guid.TryParse(sharerUserId, out var sharer)
                ? new NotificationDestination(
                    NotificationTarget.Invitation,
                    Offer: new InvitationOffer(sharedItemKind, share, sharer))
                : null;

    /// <summary>
    /// The kind that path segment names, read through the mapping the server writes the path with so
    /// the two cannot drift apart. Null for a kind this build does not know - a newer server - and for
    /// a shared position, which is somebody's whereabouts rather than a thing an account can be given.
    /// </summary>
    private static SharedItemKind? KindOf(string segment) => Orbit.Core.Notifications.SharedItemPath.KindOf(segment) switch
    {
        CoreSharedItemKind.Note => SharedItemKind.Note,
        CoreSharedItemKind.TaskList => SharedItemKind.TaskList,
        CoreSharedItemKind.CalendarEvent => SharedItemKind.CalendarEvent,
        CoreSharedItemKind.Inventory => SharedItemKind.Inventory,
        CoreSharedItemKind.Place => SharedItemKind.Place,
        _ => null
    };
}
