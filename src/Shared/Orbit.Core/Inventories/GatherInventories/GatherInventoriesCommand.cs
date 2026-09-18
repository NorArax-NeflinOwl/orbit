using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.GatherInventories;

/// <summary>
/// Says which shelves this one gathers - see Inventory.GathersInventoryIds, and
/// Orbit.Core.Tasks.LinkTaskListToInventory.LinkTaskListToInventoryCommand, which is the same shape of
/// decision about the same pair of things.
///
/// Its own command rather than part of the save, for the reason filing and pinning are their own: it
/// changes what the shelf is read alongside rather than what is on it, and a save is the whole
/// inventory - so a client that had never heard of gathering would scatter every group it touched.
///
/// One command for the whole membership rather than add and remove. What somebody arranged is an
/// ordered list, and two commands agreeing about who may and about where a member lands is how a rule
/// comes to hold on one of them and not the other.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record GatherInventoriesCommand(
    Guid UserId, Guid InventoryId, IReadOnlyList<Guid> InventoryIds) : IRequest<bool>;
