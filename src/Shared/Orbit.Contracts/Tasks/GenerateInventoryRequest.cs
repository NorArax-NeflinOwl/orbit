using Orbit.Contracts.Inventories;

namespace Orbit.Contracts.Tasks;

/// <summary>
/// What to build when a task list is turned into the storage its work needs - see
/// Orbit.Core.Tasks.GenerateInventoryFromTaskList.GenerateInventoryFromTaskListCommand.
///
/// Both halves are optional, and the whole body is: a client that asks for a storage without saying
/// anything about it gets the list's own title and the restock list every inventory starts with, which
/// is what generating one has always done.
/// </summary>
/// <param name="Name">
/// What the storage is called. Null or blank is the task list's own title - the answer the form offers
/// before anybody changes it.
/// </param>
/// <param name="RestockList">
/// How the "Restock supplies" list this storage keeps should behave - see RestockListSettingsDto. Null
/// leaves it at the defaults.
/// </param>
public sealed record GenerateInventoryRequest(string? Name = null, RestockListSettingsDto? RestockList = null);
