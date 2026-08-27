using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.RaiseStockShortfalls;

/// <summary>
/// Puts whatever this task list's work is short of onto its warehouse's standing restock list, where the
/// daily reminder will bring it up. Returns how many entries were added - zero when nothing was short,
/// or when everything short was already waiting there.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record RaiseStockShortfallsCommand(Guid UserId, Guid TaskListId) : IRequest<int>;
