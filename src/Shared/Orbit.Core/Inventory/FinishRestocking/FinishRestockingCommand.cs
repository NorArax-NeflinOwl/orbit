using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.FinishRestocking;

/// <summary>
/// "Everything on this list is done": crosses off what is left of a restock list and brings its whole
/// warehouse up to the levels it is meant to hold. Returns how many shelf items that moved - zero for a
/// list no warehouse tracks, which is every ordinary list.
/// </summary>
[ClientAction(ClientActionCategory.Save)]
public sealed record FinishRestockingCommand(Guid UserId, Guid TaskListId) : IRequest<int>;
