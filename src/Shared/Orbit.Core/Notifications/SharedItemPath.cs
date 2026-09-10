namespace Orbit.Core.Notifications;

/// <summary>
/// What each shareable kind is called inside an address - the segment in
/// <c>/invitation/{kind}/{shareId}/{sharerUserId}</c> and in <c>/api/shares/{kind}/{shareId}</c>.
///
/// One place because five read or write it: the notifier that builds the path, the endpoint that
/// answers it, the page that reads it back, the client that asks for an offer, and the conversation
/// that settles the notification after accepting there. Five copies of one mapping is how three of them
/// come to disagree.
///
/// <b>These names are stable.</b> They sit in notification rows already written and in paths already
/// handed to a phone, so renaming one orphans every invitation that carried it - the same rule
/// SharedItemType follows for OL_PS_ITEMTYPE.
/// </summary>
public static class SharedItemPath
{
    public static string For(SharedItemKind kind) => kind switch
    {
        SharedItemKind.Note => "note",
        SharedItemKind.TaskList => "tasklist",
        SharedItemKind.CalendarEvent => "event",
        SharedItemKind.Inventory => "inventory",
        SharedItemKind.Place => "place",
        _ => "location"
    };

    /// <summary>
    /// The kind that segment names, or null for anything this build does not know - which is a client
    /// or a server newer than this one, and a thing to survive rather than to fail on.
    /// </summary>
    public static SharedItemKind? KindOf(string? path) => path?.ToLowerInvariant() switch
    {
        "note" => SharedItemKind.Note,
        "tasklist" => SharedItemKind.TaskList,
        "event" => SharedItemKind.CalendarEvent,
        "inventory" => SharedItemKind.Inventory,
        "location" => SharedItemKind.Location,
        "place" => SharedItemKind.Place,
        _ => null
    };
}
