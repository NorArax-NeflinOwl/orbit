namespace Orbit.Core.Tasks;

/// <summary>
/// How much a whole task list matters, set by its owner. Declaration order is significant: sorting by
/// priority puts High first, and the underlying int is what orders it.
/// </summary>
public enum TaskListPriority
{
    Low,

    /// <summary>What a list gets unless someone says otherwise - most lists are just lists.</summary>
    Normal,

    High
}
