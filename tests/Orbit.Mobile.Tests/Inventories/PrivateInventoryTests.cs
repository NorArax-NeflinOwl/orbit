using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Inventory;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Inventories;

/// <summary>
/// What a private inventory shows on the inventory screen while private things are locked. The
/// counterpart of PrivateNoteTests and PrivateTaskListTests, and the last of the three: the shelf used
/// to be left out of the search entirely and named in full on the list, which is both halves of the
/// promise broken at once.
/// </summary>
public sealed class PrivateInventoryTests
{
    [Fact]
    public void A_private_inventory_says_nothing_about_itself_while_locked()
    {
        var row = Describe(
            new LocalInventory { Name = "Bank papers", IsPrivate = true }, privateItemsAreUnlocked: false);

        Assert.True(row.IsHidden);
        Assert.Equal("Private", row.DisplayName);
        Assert.False(row.CanBeOpened);
    }

    [Fact]
    public void The_same_inventory_reads_normally_once_unlocked()
    {
        var row = Describe(
            new LocalInventory { Name = "Bank papers", IsPrivate = true }, privateItemsAreUnlocked: true);

        Assert.False(row.IsHidden);
        Assert.Equal("Bank papers", row.DisplayName);
        Assert.True(row.CanBeOpened);
    }

    [Fact]
    public void An_ordinary_inventory_is_never_hidden()
    {
        var row = Describe(new LocalInventory { Name = "Pantry" }, privateItemsAreUnlocked: false);

        Assert.False(row.IsHidden);
        Assert.Equal("Pantry", row.DisplayName);
    }

    private static InventoryRow Describe(LocalInventory inventory, bool privateItemsAreUnlocked)
        => InventoryRow.From(
            inventory, hasUnsentChanges: false, FixedNetworkStatus.Online,
            new Translations(new InMemoryLanguageStore()), privateItemsAreUnlocked);
}
