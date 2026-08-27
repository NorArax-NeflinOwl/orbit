namespace Orbit.Core.Abstractions;

/// <summary>
/// How much something matters, set by its owner. The same three levels for a task list, a note and a
/// calendar event: one word means one thing wherever somebody reads it, and one filter can be offered
/// against all of them.
///
/// Declaration order is significant: sorting by priority puts High first, and the underlying int is
/// what orders it.
/// </summary>
public enum ItemPriority
{
    Low,

    /// <summary>What something gets unless someone says otherwise - most things are just things.</summary>
    Normal,

    High
}
