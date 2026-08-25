namespace Orbit.Core.Sharing;

/// <summary>What a public link points at - a kind plus the item's own id, since notes, task lists, events and warehouses each live in their own table.</summary>
public sealed record SharedItemReference(SharedItemType ItemType, Guid ItemId);

/// <summary>The kinds of item a public link can be made for. Chat messages and locations are deliberately absent: both are only ever readable by their two parties.</summary>
public enum SharedItemType
{
    Note,
    TaskList,
    CalendarEvent,
    Warehouse
}
