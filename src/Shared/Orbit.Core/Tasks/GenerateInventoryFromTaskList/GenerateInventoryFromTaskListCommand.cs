using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;

namespace Orbit.Core.Tasks.GenerateInventoryFromTaskList;

/// <summary>
/// Makes an inventory holding one entry per distinct thing a task list's work calls for, and points the
/// list at it. Returns the new inventory's id, or null when there is no such list.
/// </summary>
/// <param name="Name">
/// What the storage is called. Null or blank is the list's own title, which is what generating one has
/// always used and what the form offers before anybody changes it.
/// </param>
/// <param name="RestockList">
/// How the "Restock supplies" list this storage keeps should behave - see <see cref="RestockListSettings"/>.
/// Null leaves it at the defaults. Written before the shelf is stocked, so the first errands the list
/// raises already follow it rather than being raised the default way and corrected afterwards.
/// </param>
[ClientAction(ClientActionCategory.Save)]
public sealed record GenerateInventoryFromTaskListCommand(
    Guid UserId, Guid TaskListId, string? Name = null, RestockListSettings? RestockList = null) : IRequest<Guid?>;
