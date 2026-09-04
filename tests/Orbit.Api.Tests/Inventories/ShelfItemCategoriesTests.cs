using Orbit.Api.Tests.TestDoubles;
using Orbit.Core;
using Orbit.Core.Inventories;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Inventories;

/// <summary>
/// A shelf item is filed under as many words as apply, the way a task entry already was. The single
/// box it replaced asked somebody stocking a shelf whether the flour was "baking" or "dry goods" when
/// it is plainly both.
/// </summary>
public sealed class ShelfItemCategoriesTests
{
    [Fact]
    public void An_item_can_be_filed_under_several_things()
    {
        var item = Flour(["Baking", "Dry goods"]);

        Assert.Equal(["Baking", "Dry goods"], item.Categories);
    }

    [Fact]
    public void The_words_are_tidied_the_way_a_task_entrys_are()
    {
        // Trimmed, blanks dropped, repeats folded case-insensitively - one rule for both, so a list of
        // words does not mean two different things depending on which screen typed it.
        var item = Flour([" Baking ", "", "baking", "Dry goods"]);

        Assert.Equal(["Baking", "Dry goods"], item.Categories);
    }

    [Fact]
    public void An_item_filed_under_nothing_carries_an_empty_list_rather_than_a_blank_word()
    {
        var item = Flour([""]);

        Assert.Empty(item.Categories);
    }

    [Fact]
    public void Saving_replaces_them_rather_than_adding_to_them()
    {
        var item = Flour(["Baking"]);

        item.Update(
            "Flour", "Food", ["Dry goods"], 2, 1, InventoryUnit.Kilogram, null, NotificationChannel.None);

        Assert.Equal(["Dry goods"], item.Categories);
    }

    [Fact]
    public void One_of_them_being_too_long_is_refused_like_the_single_one_was()
    {
        Assert.Throws<Orbit.Core.Abstractions.InvalidRequestException>(
            () => Flour(["Baking", new string('x', StoredTextLimits.Category + 1)]));
    }

    private static InventoryItem Flour(IReadOnlyList<string> categories)
        => InventoryItem.Create(
            Guid.NewGuid(), "Flour", "Food", categories, 2, 1, InventoryUnit.Kilogram, null,
            NotificationChannel.None);
}
