namespace Orbit.Mobile.Data;

/// <summary>
/// A task entry that is an appointment made on this phone, waiting for the server to name the event it
/// stands for.
///
/// A Calendar entry carries its event's id, and that id is the <b>server's</b> - it is what a second
/// device, and the server itself, use to find the appointment. An event created with no connection has
/// no such id yet, only this phone's own. Rather than refuse the whole thing until there is a
/// connection, the event is written locally like everything else and the pairing is remembered here;
/// when the calendar's outbox flushes and the event is given a server id,
/// <see cref="Orbit.Mobile.Sync.PendingCalendarLinkResolver"/> writes that id onto the entry and this
/// row goes away.
///
/// Its own table rather than a field on the entry, because the entry is a <c>TaskItemDto</c> - a
/// contract shared with the server, which has no business knowing about ids that exist only here.
/// </summary>
public sealed class PendingCalendarLink
{
    /// <summary>The entry's own id, which is what makes a row unique: one entry is one appointment.</summary>
    public Guid TaskItemId { get; set; }

    /// <summary>The list the entry is on, so the resolver can find and rewrite it.</summary>
    public Guid TaskListLocalId { get; set; }

    /// <summary>This phone's id for the event, which is what gains a server id when the calendar syncs.</summary>
    public Guid CalendarEventLocalId { get; set; }
}
