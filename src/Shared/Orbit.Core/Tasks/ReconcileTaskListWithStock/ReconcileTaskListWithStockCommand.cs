using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.ReconcileTaskListWithStock;

/// <summary>
/// Brings a task list and the warehouse it is measured against back into step, in both directions: what
/// the shelf already covers is crossed off, and anything the shelf holds that the list never mentions is
/// added to it. Nothing happens when there is no such list, or no warehouse chosen for it.
/// </summary>
[ClientAction(ClientActionCategory.Save)]
public sealed record ReconcileTaskListWithStockCommand(Guid UserId, Guid TaskListId) : IRequest<StockReconciliation>;

/// <summary>What one reconciliation did, so the screen can say it rather than only redraw.</summary>
/// <param name="CrossedOff">Entries the shelf turned out to cover, and so finished.</param>
/// <param name="Added">Products that were on the shelf but on no list, and so put on one.</param>
public sealed record StockReconciliation(int CrossedOff, int Added)
{
    public static readonly StockReconciliation Nothing = new(0, 0);
}
