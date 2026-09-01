namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// What order the panel pricing a list against a warehouse lists what it needs in. Its own set rather
/// than the order the list itself is read in: the rows there are products and shortfalls, not things to
/// tick. The same four Orbit.Web offers - see its StockCheckOrder.
/// </summary>
public enum StockCheckOrder
{
    /// <summary>The order the work asks for them, which is the order the lists are written in.</summary>
    AsCounted,
    Alphabetical,
    ReverseAlphabetical,

    /// <summary>What the shelf does not cover first - the only rows anybody has to do anything about.</summary>
    ShortFirst
}

/// <summary>
/// How one person reads one checklist on this device: whether the panel that prices it against a
/// warehouse is in the way, and what order that panel lists things in. They travel together because
/// they are answers to the same question and are saved by the same write.
/// </summary>
public sealed record ChecklistReading(
    bool IsStockCheckFolded = false, StockCheckOrder StockOrder = StockCheckOrder.AsCounted)
{
    public static readonly ChecklistReading Default = new();
}

/// <summary>
/// Remembers, per task list, how it should open. Held on the device rather than on the server, as
/// Orbit.Web holds it in the browser and for the same reason: it is how one person reads one page on
/// one device, and it says nothing about the list itself.
/// </summary>
public interface IChecklistReadingStore
{
    ChecklistReading Read(Guid taskListLocalId);

    void Write(Guid taskListLocalId, ChecklistReading reading);
}
