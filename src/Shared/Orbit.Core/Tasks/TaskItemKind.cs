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
    Calendar
}
