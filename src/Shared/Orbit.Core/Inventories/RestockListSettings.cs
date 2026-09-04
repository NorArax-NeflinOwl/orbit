using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;

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
/// stock reminder arriving while everybody is asleep is one nobody acts on. Means nothing while
/// <paramref name="RemindDaily"/> is off, which is why the field it is edited in is greyed out there.
/// </param>
/// <param name="IsEnabled">
/// Whether this inventory keeps a restock list at all. True for everybody who has not said otherwise,
/// since that is what Orbit has always done.
///
/// Turning it off **deletes** the managed list and everything on it, and stops anything creating another
/// - a shelf nobody wants restocked should not leave a list behind for somebody to wonder about. Turning
/// it back on builds a fresh one, with the standing reminder, the way the first one was built. That is
/// destructive on purpose and is why the editor asks before it saves.
/// </param>
/// <param name="RemindDaily">
/// Whether the list carries the standing "Update stock levels" reminder that comes back every day and
/// shows on the calendar. Off leaves the list itself alone: products dropping below their minimum still
/// raise their own errands, there is simply nothing arriving each morning to ask about the whole shelf.
/// </param>
/// <param name="OnlyCheckedRegularly">
/// A second narrowing, and a blunter one: only the products somebody marked as ones to look at every
/// round (see <see cref="InventoryItem.IsCheckedRegularly"/>) are asked for, whatever the shelf says
/// about the rest.
///
/// False - the default - leaves the answer as it was: everything the rule above lets through, which
/// includes anything that has dropped below its own minimum. True is for a shelf whose counts nobody
/// keeps up to date, where the list is a round rather than a report: the milk, the batteries, the
/// things you look at rather than count.
/// </param>
/// <param name="ReminderChannel">
/// Where the standing "Update stock levels" reminder is said - the banner, an email, the phone, or
/// nowhere but the in-app feed. Means nothing while <paramref name="RemindDaily"/> is off, for the same
/// reason the hour does.
/// </param>
/// <param name="ListPriority">
/// How much the generated list matters, which is the priority the "Restock supplies - X" list is created
/// with and kept at. A task list carries a priority and a task item does not, so this is the only place
/// the restock work can be marked as mattering more or less than the rest.
/// </param>
public sealed record RestockListSettings(
    bool OnlyLinkedWithDueDate,
    TimeOnly RefreshTimeOfDay,
    bool IsEnabled = true,
    bool RemindDaily = true,
    ItemPriority ListPriority = ItemPriority.Normal,
    bool OnlyCheckedRegularly = false,
    NotificationChannel ReminderChannel = NotificationChannel.Push)
{
    public static readonly TimeOnly DefaultRefreshTimeOfDay = new(9, 0);

    /// <summary>What an inventory has before anybody changes anything - the rule Orbit has always used.</summary>
    public static readonly RestockListSettings Default = new(OnlyLinkedWithDueDate: false, DefaultRefreshTimeOfDay);
}
