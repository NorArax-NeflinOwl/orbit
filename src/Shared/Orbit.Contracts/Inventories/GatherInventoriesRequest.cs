namespace Orbit.Contracts.Inventories;

/// <summary>
/// Which shelves a group shelf gathers, in the order somebody arranged them - see
/// Orbit.Core.Inventories.GatherInventories.GatherInventoriesCommand. The whole membership rather than
/// one added or removed: what was arranged is an ordered list, and sending a change to it would make the
/// server guess where a new member landed.
///
/// An empty list is how a group stops being one.
/// </summary>
public sealed record GatherInventoriesRequest(IReadOnlyList<Guid> InventoryIds);
