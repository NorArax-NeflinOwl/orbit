using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Inventory;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// What a list card offers behind its three dots - the phone's half of Orbit.Web's ObjectMenu.
///
/// The rules are the point rather than the markup: an entry is left out where it does not apply rather
/// than drawn spent, so what these pin down is which cards end up with no menu at all. Getting that
/// wrong in the permissive direction is the failure that matters - a Delete offered on somebody else's
/// inventory is a press that reads as destroying their shelf.
/// </summary>
public sealed class CardMenuTests
{
    [Fact]
    public void An_inventory_of_your_own_can_be_shared_and_deleted()
    {
        var row = RowFor(new LocalInventory { ServerId = Guid.NewGuid(), Name = "Kitchen" });

        Assert.True(row.CanBeShared);
        Assert.False(row.IsSharedWithMe);
        Assert.True(row.HasCardMenu);
    }

    /// <summary>
    /// The server keeps no readable copy of a private inventory, so there is nothing to hand anybody -
    /// which is what makes it private. Deleting it is still this reader's to do.
    /// </summary>
    [Fact]
    public void A_private_inventory_is_offered_to_nobody()
    {
        var row = RowFor(new LocalInventory { ServerId = Guid.NewGuid(), Name = "Kitchen", IsPrivate = true });

        Assert.False(row.CanBeShared);
        Assert.True(row.HasCardMenu);
    }

    /// <summary>One the server has not seen yet has no id to share, however new the phone thinks it is.</summary>
    [Fact]
    public void An_inventory_the_server_has_never_seen_cannot_be_handed_on()
    {
        var row = RowFor(new LocalInventory { Name = "Kitchen" });

        Assert.False(row.CanBeShared);
    }

    [Fact]
    public void An_inventory_shared_read_only_can_be_neither_handed_on_nor_deleted()
    {
        var row = RowFor(new LocalInventory
        {
            ServerId = Guid.NewGuid(),
            Name = "Their kitchen",
            IsShared = true,
            AccessLevel = "ReadOnly"
        });

        Assert.False(row.CanBeShared);
        Assert.True(row.IsSharedWithMe);

        // So its card carries no three dots at all, rather than three dots that open on nothing.
        Assert.False(row.HasCardMenu);
    }

    /// <summary>
    /// A share at Share or above may be handed on - the same rule the server enforces, asked through
    /// Orbit.Core rather than by comparing the level to a string. Deleting it is still not this
    /// reader's to do, so the card offers the one and not the other.
    /// </summary>
    [Fact]
    public void An_inventory_shared_with_permission_to_pass_on_offers_that_and_not_deleting()
    {
        var row = RowFor(new LocalInventory
        {
            ServerId = Guid.NewGuid(),
            Name = "Their kitchen",
            IsShared = true,
            AccessLevel = "Share"
        });

        Assert.True(row.CanBeShared);
        Assert.True(row.IsSharedWithMe);
        Assert.True(row.HasCardMenu);
    }

    private static InventoryRow RowFor(LocalInventory inventory) => InventoryRow.From(
        inventory, hasUnsentChanges: false, FixedNetworkStatus.Online,
        new Translations(new InMemoryLanguageStore()));
}
