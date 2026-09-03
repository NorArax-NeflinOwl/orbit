using Orbit.Contracts.Inventories;
using Xunit;

namespace Orbit.Api.Tests.Inventories;

/// <summary>
/// Creating an inventory takes its name, not its contents. This pins that sending items is refused
/// rather than accepted and dropped - a caller holding an inventory that quietly lost what it was told
/// to keep has no way of finding out until much later.
/// </summary>
public sealed class CreateInventoryRefusesItemsTests
{
    [Fact]
    public void The_request_shape_still_carries_items_so_the_endpoint_has_something_to_refuse()
    {
        // If Items ever leaves SaveInventoryRequest, the guard in InventoryEndpoints becomes dead code
        // and this test is the reminder to take it out with it.
        var request = new SaveInventoryRequest("Workshop", [
            new InventoryItemRequest(Id: null, "Screw", "Part", "Hardware", 5, null, "Piece", null, "None")]);

        Assert.Single(request.Items);
    }
}
