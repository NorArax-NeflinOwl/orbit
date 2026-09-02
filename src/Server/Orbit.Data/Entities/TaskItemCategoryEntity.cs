namespace Orbit.Data.Entities;

/// <summary>
/// One category a <see cref="TaskItemEntity"/> is filed under. A table rather than a column on the
/// entry itself, for the same reason <see cref="TaskItemTaskListLinkEntity"/> is one: an entry can
/// carry several - see Orbit.Core.Tasks.TaskItem.Categories.
/// </summary>
public sealed class TaskItemCategoryEntity
{
    public Guid TaskItemId { get; set; }

    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Where this category sits among the entry's. Stored for the same reason the entry's own position
    /// is: a save deletes the rows and writes them again, so without it the order came back as whatever
    /// the database happened to hold.
    /// </summary>
    public int Position { get; set; }
}
