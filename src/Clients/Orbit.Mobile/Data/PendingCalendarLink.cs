namespace Orbit.Mobile.Data;

/// <summary>
/// A task entry that is an appointment made on this phone, waiting for the server to name the event it
/// stands for.
///
/// A Calendar entry carries its event's <b>server</b> id, and an event created with no connection has
/// none yet, only this phone's own. Rather than refuse the whole thing until there is a connection, the
/// event is written locally like everything else and the pairing is remembered here; when the
/// calendar's outbox flushes and the event is given a server id,
/// <see cref="Orbit.Mobile.Sync.PendingCalendarLinkResolver"/> writes that id onto the entry and this
/// row goes away.
///
/// <b>Keyed on the event, not on the entry</b>, and that is the whole difficulty. An entry created
/// offline has no id of its own: <c>TaskItemDto.Id</c> stays empty until the list is pushed and the
/// server mints one (see <c>TaskItem.Create</c>, which ignores whatever the client sent). A link keyed
/// on that id would stop matching the moment the list synced - which is exactly when it is needed. The
/// event's own local id, on the other hand, is this phone's and never changes.
///
/// Its own table rather than a field on the entry, because the entry is a <c>TaskItemDto</c> - a
/// contract shared with the server, which has no business knowing about ids that exist only here.
/// </summary>
public sealed class PendingCalendarLink
{
    /// <summary>This phone's id for the event, which is what gains a server id when the calendar syncs.</summary>
    public Guid CalendarEventLocalId { get; set; }

    /// <summary>The list the entry is on, so the resolver knows where to look for it.</summary>
    public Guid TaskListLocalId { get; set; }

    /// <summary>
    /// What the entry said when the appointment was made, which is also the event's title. Used to pick
    /// the entry out again: an id cannot be relied on, but a list rarely holds two appointments with the
    /// same words and no event yet - and where it does, either pairing gives each entry an appointment
    /// that says what it says.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
