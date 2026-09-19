using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventories;
using Orbit.Core.Inventories.GatherInventories;
using Xunit;

namespace Orbit.Api.Tests.Inventories;

/// <summary>
/// A shelf that gathers other shelves. The user's rule, 2026-09-18: "one entry on the list of
/// inventories, holding smaller inventories inside it, which can answer to different task lists".
///
/// Gathering rather than containing - a member keeps its own rows, its own restock list and its own tie
/// to a list, and it stays on the list of inventories where it was. The group is a way of reading
/// several at once, which is why nothing here moves anything.
/// </summary>
public sealed class GroupInventoryTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private readonly InventoryTestContext _context = new();

    private GatherInventoriesCommandHandler Handler()
        => new(_context.InventoryRepository, new InventoryGroups(_context.InventoryRepository));

    private Task<bool> GatherAsync(Guid inventoryId, params Guid[] members)
        => Handler().HandleAsync(
            new GatherInventoriesCommand(UserId, inventoryId, members), CancellationToken.None);

    private Inventory Stored(Guid inventoryId)
        => _context.InventoryRepository.GetByIdAsync(UserId, inventoryId, CancellationToken.None)
            .GetAwaiter().GetResult()!;

    [Fact]
    public async Task A_shelf_gathers_the_shelves_it_is_given_in_the_order_they_were_given()
    {
        var kitchen = _context.AddInventory(UserId, "Kitchen");
        var fridge = _context.AddInventory(UserId, "Fridge");
        var pantry = _context.AddInventory(UserId, "Pantry");

        Assert.True(await GatherAsync(kitchen, fridge, pantry));

        Assert.Equal([fridge, pantry], Stored(kitchen).GathersInventoryIds);
        Assert.True(Stored(kitchen).IsGroup);
    }

    /// <summary>An empty membership is how a group stops being one, and nothing is lost by it.</summary>
    [Fact]
    public async Task A_group_stops_being_one_when_it_gathers_nothing()
    {
        var kitchen = _context.AddInventory(UserId, "Kitchen");
        var fridge = _context.AddInventory(UserId, "Fridge");
        await GatherAsync(kitchen, fridge);

        Assert.True(await GatherAsync(kitchen));

        Assert.False(Stored(kitchen).IsGroup);
        // The member is still a shelf of its own - gathering never moved it anywhere.
        Assert.NotNull(Stored(fridge));
    }

    /// <summary>
    /// A ring is the one thing gathering has to refuse: everything that walks a group would walk it
    /// forever, and only a save can see both sides of it.
    /// </summary>
    [Fact]
    public async Task Two_shelves_cannot_gather_each_other()
    {
        var kitchen = _context.AddInventory(UserId, "Kitchen");
        var fridge = _context.AddInventory(UserId, "Fridge");
        await GatherAsync(kitchen, fridge);

        Assert.False(await GatherAsync(fridge, kitchen));

        Assert.Empty(Stored(fridge).GathersInventoryIds);
    }

    /// <summary>And a longer ring, which is the same fault two shelves further apart.</summary>
    [Fact]
    public async Task Nor_can_three_of_them_round_a_circle()
    {
        var kitchen = _context.AddInventory(UserId, "Kitchen");
        var fridge = _context.AddInventory(UserId, "Fridge");
        var freezer = _context.AddInventory(UserId, "Freezer");
        await GatherAsync(kitchen, fridge);
        await GatherAsync(fridge, freezer);

        Assert.False(await GatherAsync(freezer, kitchen));
    }

    /// <summary>A shelf gathering itself is a shelf read forever, and not something anybody means.</summary>
    [Fact]
    public async Task A_shelf_cannot_gather_itself()
    {
        var kitchen = _context.AddInventory(UserId, "Kitchen");

        Assert.False(await GatherAsync(kitchen, kitchen));
    }

    /// <summary>
    /// A group is an arrangement of shelves its owner has. Somebody else's would put rows on their page
    /// that its owner can take away without knowing.
    /// </summary>
    [Fact]
    public async Task Somebody_elses_shelf_is_not_theirs_to_gather()
    {
        var kitchen = _context.AddInventory(UserId, "Kitchen");
        var theirs = _context.AddInventory(Guid.NewGuid(), "Their pantry");

        Assert.False(await GatherAsync(kitchen, theirs));
    }

    [Fact]
    public async Task And_a_recipient_does_not_rearrange_the_owners_group()
    {
        var ownerId = Guid.NewGuid();
        var kitchen = _context.AddInventory(ownerId, "Kitchen");
        var fridge = _context.AddInventory(ownerId, "Fridge");
        _context.AddAcceptedShare(kitchen, ownerId, UserId, Orbit.Core.Abstractions.ShareAccessLevel.CanEdit);

        Assert.False(await GatherAsync(kitchen, fridge));
    }

    /// <summary>
    /// What a group stands for, walked: its own members and then what each of them gathers. A shelf
    /// reached twice is listed once - two members both gathering the same cupboard is one cupboard.
    /// </summary>
    [Fact]
    public async Task A_group_stands_for_what_it_gathers_however_deep_that_goes()
    {
        var kitchen = _context.AddInventory(UserId, "Kitchen");
        var fridge = _context.AddInventory(UserId, "Fridge");
        var freezer = _context.AddInventory(UserId, "Freezer");
        var cupboard = _context.AddInventory(UserId, "Cupboard");
        await GatherAsync(fridge, freezer, cupboard);
        await GatherAsync(kitchen, fridge, cupboard);

        var gathered = await new InventoryGroups(_context.InventoryRepository)
            .GatheredByAsync(UserId, Stored(kitchen), CancellationToken.None);

        Assert.Equal(
            ["Fridge", "Freezer", "Cupboard"], gathered.Select(inventory => inventory.Name));
    }

    /// <summary>An ordinary shelf is asked nothing at all - nearly every shelf is one.</summary>
    [Fact]
    public async Task A_shelf_that_gathers_nothing_stands_for_nothing_but_itself()
    {
        var kitchen = _context.AddInventory(UserId, "Kitchen");

        Assert.Empty(await new InventoryGroups(_context.InventoryRepository)
            .GatheredByAsync(UserId, Stored(kitchen), CancellationToken.None));
    }
}
