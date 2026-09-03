using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.ReconcileRestockList;

/// <summary>
/// Settles the finished errands on a restock list against the inventory behind it: each one tops its
/// shelf item up to the minimum and then leaves the list.
///
/// A command rather than something the read path does quietly, because it changes two things. The
/// checklist screen asks for it when it opens a restock list, which is what heals a list that has been
/// accumulating crossed-off errands - the save path settles them as they are ticked, but a list ticked
/// before that existed has nobody to settle it.
/// </summary>
[ClientAction(ClientActionCategory.Save)]
public sealed record ReconcileRestockListCommand(Guid UserId, Guid TaskListId) : IRequest<RestockOutcome>;
