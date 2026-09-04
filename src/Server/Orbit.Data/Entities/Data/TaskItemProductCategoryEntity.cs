namespace Orbit.Data.Entities;

/// <summary>
/// One category the product a <see cref="TaskItemEntity"/> describes is filed under - see
/// Orbit.Core.Tasks.TaskItemProduct.Categories.
///
/// A second table beside <see cref="TaskItemCategoryEntity"/> rather than a share of it, because the two
/// answer different questions about the same row: that one says what the *entry* is about - "shopping",
/// "the car" - and this one says what the *thing it asks for* is filed under once it reaches a shelf.
/// An errand filed under "shopping" can perfectly well be asking for something filed under "baking".
/// </summary>
public sealed class TaskItemProductCategoryEntity
{
    public Guid TaskItemId { get; set; }

    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Where this category sits among the product's. Stored for the same reason every other position
    /// here is: a save deletes the rows and writes them again, so without it the order came back as
    /// whatever the database happened to hold.
    /// </summary>
    public int Position { get; set; }
}
