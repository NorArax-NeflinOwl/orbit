using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.RestockListSettingsAccess;

/// <summary>
/// How this inventory's restock list is built and when it comes round. Null when the inventory is not
/// one the caller may read - the same silence every other inventory query keeps.
/// </summary>
public sealed record GetRestockListSettingsQuery(Guid UserId, Guid InventoryId) : IRequest<RestockListSettings?>;

/// <summary>
/// Changes those settings and rebuilds the list to match, because the two are one act: choosing what the
/// list should ask for and then leaving it asking for something else would make the setting look broken.
/// Answers what the rebuild moved.
/// </summary>
[ClientAction(ClientActionCategory.Save)]
public sealed record SaveRestockListSettingsCommand(Guid UserId, Guid InventoryId, RestockListSettings Settings)
    : IRequest<RestockRefreshOutcome>;

/// <summary>
/// Rebuilds the list against the settings it already has - the Refresh button. Its own request because
/// it is what somebody presses when the world changed rather than the settings: a task somewhere else
/// gained a due date, or a shelf was counted by hand.
/// </summary>
[ClientAction(ClientActionCategory.Save)]
public sealed record RefreshRestockListCommand(Guid UserId, Guid InventoryId) : IRequest<RestockRefreshOutcome>;
