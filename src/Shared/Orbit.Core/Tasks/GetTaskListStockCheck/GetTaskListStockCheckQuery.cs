using Orbit.Core.Abstractions;
using Orbit.Core.Tasks.StockCheck;

namespace Orbit.Core.Tasks.GetTaskListStockCheck;

/// <summary>Whether the work on a task list - and everything linked below it - can be done out of the inventory it points at.</summary>
public sealed record GetTaskListStockCheckQuery(Guid UserId, Guid TaskListId) : IRequest<TaskListStockCheck?>;
