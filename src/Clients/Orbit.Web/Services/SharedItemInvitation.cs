using Orbit.Core.Notifications;

namespace Orbit.Web.Services;

/// <summary>
/// Where an invitation's thing is read once it is the reader's. The kind itself is read off the address
/// by SharedItemPath, which both clients and the server share; this is the web's own half - the routes
/// each kind lives at, which only this client knows.
/// </summary>
public static class SharedItemInvitation
{
    /// <summary>Where the thing is read once it is the reader's - and where accepting leads.</summary>
    public static string SectionFor(SharedItemKind kind) => kind switch
    {
        SharedItemKind.Note => "/notes",
        SharedItemKind.TaskList => "/tasks",
        SharedItemKind.CalendarEvent => "/calendar",
        SharedItemKind.Inventory => "/inventory",
        _ => "/map"
    };

    /// <summary>
    /// The thing itself, once it is the reader's. A path segment for four of the five kinds and a query
    /// for a place: a place is met on the map rather than on a page of its own, so its id says which pin
    /// to open on - see MapPage.Place, and SharedItemNotifier.AddressOf, which says the same on the
    /// server for the notification's own link.
    /// </summary>
    public static string AddressOf(SharedItemKind kind, Guid itemId)
        => kind == SharedItemKind.Place ? $"/map?place={itemId}" : $"{SectionFor(kind)}/{itemId}";
}
