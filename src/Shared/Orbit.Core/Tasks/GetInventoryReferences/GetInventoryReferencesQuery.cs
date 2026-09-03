using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.GetInventoryReferences;

/// <summary>
/// What each of a list's inventory errands is actually about: the shelf item behind it, and any other
/// list asking for the same thing.
///
/// A query of its own rather than fields on the task list, because none of it belongs to the list. The
/// shelf item lives in an inventory the reader may or may not still hold, and the other lists are a fact
/// about the whole account - both would have to be looked up on every read of every list to sit on the
/// DTO, and they are wanted on one screen.
/// </summary>
public sealed record GetInventoryReferencesQuery(Guid UserId, Guid TaskListId)
    : IRequest<IReadOnlyList<InventoryReference>>;

/// <summary>
/// One errand's context. <paramref name="AlsoAskedForBy"/> is empty in the ordinary case - a shelf item
/// has one errand open at a time - and holds the others when a product has been raised from more than
/// one place, which is exactly the case worth a link on screen.
/// </summary>
public sealed record InventoryReference(
    Guid TaskItemId,
    Guid InventoryItemId,
    string InventoryItemName,
    Guid InventoryId,
    string InventoryName,
    IReadOnlyList<InventoryReferenceElsewhere> AlsoAskedForBy);

/// <summary>Another list carrying an errand about the same shelf item, and the entry on it.</summary>
public sealed record InventoryReferenceElsewhere(Guid TaskListId, string TaskListTitle, Guid TaskItemId);
