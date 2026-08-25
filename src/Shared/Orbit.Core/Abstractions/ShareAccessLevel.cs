namespace Orbit.Core.Abstractions;

/// <summary>
/// The level of access a share offer grants its recipient once accepted - shared by calendar event,
/// note, task list, and warehouse sharing (see CalendarEventShare, NoteShare, TaskListShare,
/// WarehouseShare) so all four use the same concept instead of four copies of it.
///
/// Declaration order is significant: the underlying int doubles as a rank, and nobody may ever grant
/// more than they hold. It is not the whole rule, though - see <see cref="ShareAccess.CanGrant"/>,
/// which is what the Share*CommandHandler classes actually ask. Stored by name rather than by number
/// (see NoteShareEntity.AccessLevel), so this order can change without touching a single stored row.
/// </summary>
public enum ShareAccessLevel
{
    /// <summary>The recipient can view what was shared but not change it or share it further - the default.</summary>
    ReadOnly,

    /// <summary>
    /// The recipient can't change it, but can share it further - at ReadOnly or Share, never at a level
    /// that permits editing, since a re-share can never grant more than the re-sharer themselves holds.
    /// </summary>
    Share,

    /// <summary>
    /// The recipient can edit it, and can share it further - but only ever without editing. This is the
    /// level to hand someone who should work on something without deciding who else gets to.
    /// </summary>
    EditOnly,

    /// <summary>The recipient can edit it, and re-share it at any level including this one.</summary>
    CanEdit
}

/// <summary>
/// The rules that read <see cref="ShareAccessLevel"/>. They live here rather than in each
/// Share*CommandHandler because notes, task lists, events and warehouses must answer them identically -
/// four copies of "may this person grant that" is four chances for them to drift apart.
/// </summary>
public static class ShareAccess
{
    /// <summary>
    /// Whether this level lets its holder change the thing. Asked wherever an edit is about to happen,
    /// in place of comparing to CanEdit - EditOnly permits editing too, and a check written as an
    /// equality would quietly refuse it.
    /// </summary>
    public static bool AllowsEditing(this ShareAccessLevel level)
        => level is ShareAccessLevel.EditOnly or ShareAccessLevel.CanEdit;

    /// <summary>
    /// Whether a holder of this level may hand out <paramref name="requested"/>. Three rules, in order:
    /// a read-only holder shares nothing at all; nobody grants more than they hold; and an EditOnly
    /// holder grants nothing that permits editing - which is the whole point of that level and the one
    /// rule the rank alone cannot express, since EditOnly outranks Share.
    /// </summary>
    public static bool CanGrant(this ShareAccessLevel holder, ShareAccessLevel requested)
    {
        if (holder == ShareAccessLevel.ReadOnly)
        {
            return false;
        }

        if (requested > holder)
        {
            return false;
        }

        return holder != ShareAccessLevel.EditOnly || !requested.AllowsEditing();
    }
}
