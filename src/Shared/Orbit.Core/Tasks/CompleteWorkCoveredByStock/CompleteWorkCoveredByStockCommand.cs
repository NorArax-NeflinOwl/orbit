using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.CompleteWorkCoveredByStock;

/// <summary>
/// Crosses off the work the linked warehouse already covers, and returns how many entries that was.
/// Zero when there is no such list, no warehouse chosen for it, or nothing on the shelf to cross
/// anything off with.
/// </summary>
[ClientAction(ClientActionCategory.Save)]
public sealed record CompleteWorkCoveredByStockCommand(Guid UserId, Guid TaskListId) : IRequest<int>;
