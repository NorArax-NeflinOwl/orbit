namespace Orbit.Core.Sharing;

/// <summary>What a public link points at - a kind plus the item's own id, since notes, task lists, events and inventories each live in their own table.</summary>
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
    Inventory
}
