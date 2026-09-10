namespace Orbit.Core.Sharing;

/// <summary>What a public link points at - a kind plus the item's own id, since each of these lives in a table of its own.</summary>
public sealed record SharedItemReference(SharedItemType ItemType, Guid ItemId);

/// <summary>The kinds of item a public link can be made for. Chat messages and locations are deliberately absent: both are only ever readable by their two parties.</summary>
public enum SharedItemType
{
    Note,
    TaskList,
    CalendarEvent,

    /// <summary>
    /// Keeps the old "warehouse" wording on purpose: the value is stored as text in OP_PUBLIC_SHARES
    /// and travels inside chat payloads delivered before the rename, so changing it would orphan every
    /// share link handed out so far. The type itself is Inventory everywhere else.
    /// </summary>
    Inventory,

    /// <summary>
    /// Somewhere on the map worth keeping - see Orbit.Core.Places.Place. Last rather than beside the
    /// others: the value is stored as text, but a client that reads it as a number would turn every
    /// stored "Inventory" into something else if one were inserted in the middle.
    /// </summary>
    Place
}
