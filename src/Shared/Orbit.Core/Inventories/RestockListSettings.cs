namespace Orbit.Core.Inventories;

/// <summary>
/// How an inventory's restock list is built and when it comes round. Kept beside the tracking of which
/// list an inventory owns (see <see cref="IInventoryManagedTaskListRepository"/>), because it is settings
/// for that list rather than for the inventory: an inventory with no restock list has nothing for these
/// to mean.
/// </summary>
/// <param name="OnlyLinkedWithDueDate">
/// Which products the list asks for.
///
/// False - the default - is the original rule: anything on this shelf that has dropped below its own
/// minimum. It answers "what is running out".
///
/// True narrows it to products some task with a **due date** is waiting on. It answers a different
/// question - "what do I need before Thursday" - and it is the useful one for somebody who shops against
/// a plan rather than against a shelf. A product below its minimum that nothing is waiting on is left
/// off; so is one something is waiting on with no date, because without a date there is nothing to be
/// early or late for.
/// </param>
/// <param name="RefreshTimeOfDay">
/// When the standing "Update stock levels" reminder comes round. Nine in the morning by default - a
/// stock reminder arriving while everybody is asleep is one nobody acts on.
/// </param>
public sealed record RestockListSettings(bool OnlyLinkedWithDueDate, TimeOnly RefreshTimeOfDay)
{
    public static readonly TimeOnly DefaultRefreshTimeOfDay = new(9, 0);

    /// <summary>What an inventory has before anybody changes anything - the rule Orbit has always used.</summary>
    public static readonly RestockListSettings Default = new(OnlyLinkedWithDueDate: false, DefaultRefreshTimeOfDay);
}
