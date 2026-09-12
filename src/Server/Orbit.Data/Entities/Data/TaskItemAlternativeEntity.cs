namespace Orbit.Data.Entities;

/// <summary>
/// One way a <see cref="TaskItemEntity"/> can be got done - see Orbit.Core.Tasks.TaskItem.Alternatives.
/// A table of its own because an entry has several, and an OP_ one rather than a link: a way carries
/// words the reader wrote, and is often no link to anything.
/// </summary>
public sealed class TaskItemAlternativeEntity
{
    public Guid TaskItemId { get; set; }

    /// <summary>
    /// Where this way sits among the entry's, and half the key: two ways may say the same words, so the
    /// words cannot be. Stored for the reason every child row here stores one - a save writes them again.
    /// </summary>
    public int Position { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The list this way is, or null for a line of its own. No foreign key, for the reason the entry's
    /// own links have none: a way whose list is gone reads as not done rather than as a failure.
    /// </summary>
    public Guid? LinkedTaskListId { get; set; }

    /// <summary>
    /// Whether a line of its own was ticked. Always false for a way that is a list: that answer is worked
    /// out from the list on every read, never stored - see Orbit.Core.Tasks.LinkedTaskCompletionResolver.
    /// </summary>
    public bool IsDone { get; set; }
}
