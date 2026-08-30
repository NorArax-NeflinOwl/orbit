namespace Orbit.Core.Tasks;

/// <summary>
/// What one entry on a task list is. The kind decides what else the entry carries: a calendar entry is
/// somewhere to be rather than something to fetch, so it also holds a place - see
/// <see cref="TaskItem.Location"/> - and can be tied to an actual calendar event.
///
/// On the entry rather than on the list, because a list is rarely all one or all the other: a day's
/// plan holds two errands and an appointment, and asking somebody to keep those on separate lists is
/// asking them to keep the list that matches their day in two places.
///
/// Stored by name rather than by number (see TaskItemEntity.Kind), so this order can change without
/// touching a single stored row.
/// </summary>
public enum TaskItemKind
{
    /// <summary>The ordinary entry: something to do, ticked off when it is done.</summary>
    Checklist,

    /// <summary>An appointment, which happens somewhere as well as at some time.</summary>
    Calendar,

    /// <summary>
    /// An errand about one product on a shelf: bring this back up to the level the warehouse is meant to
    /// hold. Carries <see cref="TaskItem.LinkedInventoryItemId"/>, which is what makes it that product's
    /// errand rather than a line of text that happens to mention it.
    ///
    /// The link is why this kind exists. Orbit used to recognise these entries by reading their
    /// description - "Restock: " and then a product name parsed back out of it - which meant renaming a
    /// product broke the connection, and two products whose names differed only by punctuation were the
    /// same errand. The description is now what a person reads; the link is what Orbit acts on.
    /// </summary>
    Inventory
}
