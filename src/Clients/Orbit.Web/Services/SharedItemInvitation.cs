using Orbit.Core.Notifications;

namespace Orbit.Web.Services;

/// <summary>
/// What kind of thing an invitation is about, read off its own address. The names are the ones
/// SharedItemNotifier writes into a notification's path, and they are stable: rows already written and
/// paths already handed to a phone carry them, so renaming one orphans every invitation that used it.
///
/// Its own class rather than a switch on the page: the same four names decide which endpoint answers,
/// which one accepts, and where the reader goes afterwards, and those are one fact rather than three.
/// </summary>
public static class SharedItemInvitation
{
    /// <summary>The kind that path segment names, or null for anything this build does not know.</summary>
    public static SharedItemKind? KindOf(string? path) => path?.ToLowerInvariant() switch
    {
        "note" => SharedItemKind.Note,
        "tasklist" => SharedItemKind.TaskList,
        "event" => SharedItemKind.CalendarEvent,
        "inventory" => SharedItemKind.Inventory,
        _ => null
    };

    /// <summary>Where the thing is read once it is the reader's - and where accepting leads.</summary>
    public static string SectionFor(SharedItemKind kind) => kind switch
    {
        SharedItemKind.Note => "/notes",
        SharedItemKind.TaskList => "/tasks",
        SharedItemKind.CalendarEvent => "/calendar",
        SharedItemKind.Inventory => "/inventory",
        _ => "/map"
    };
}
