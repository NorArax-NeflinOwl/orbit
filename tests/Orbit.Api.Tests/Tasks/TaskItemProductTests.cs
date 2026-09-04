using Orbit.Core;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// What an inventory entry asks for, written on the entry itself while there is no shelf item to write
/// it on - see TaskItemProduct. The rules here are all about when it is kept and when it is dropped,
/// because a description that outlives what it described is the one that goes stale.
/// </summary>
public sealed class TaskItemProductTests
{
    private static readonly TaskItemProduct Flour = TaskItemProduct.Default with
    {
        ProductType = "Dry goods",
        Categories = ["Baking", "Dry goods"],
        Quantity = 1,
        MinimumQuantity = 3,
        Unit = InventoryUnit.Kilogram,
        IsCheckedRegularly = true
    };

    private static TaskItem AnEntryAskingForFlour(TaskItemSubject? subject = null)
        => TaskItem.Create(
            "Flour", dueDateUtc: null, isCompleted: false,
            subject: subject ?? new TaskItemSubject(TaskItemKind.Inventory), product: Flour);

    [Fact]
    public void An_ordinary_entry_asks_for_nothing_in_particular()
        => Assert.Null(TaskItem.Create("Buy milk", dueDateUtc: null, isCompleted: false).Product);

    [Fact]
    public void An_inventory_entry_keeps_what_it_was_told_to_ask_for()
    {
        var entry = AnEntryAskingForFlour();

        Assert.Equal(Flour, entry.Product);
    }

    /// <summary>
    /// The same rule the subject applies to its own links: what does not belong to this kind of entry is
    /// dropped rather than refused, so changing an entry's kind loses what no longer applies instead of
    /// failing the save.
    /// </summary>
    [Fact]
    public void An_appointment_does_not_describe_a_product()
    {
        var entry = AnEntryAskingForFlour(new TaskItemSubject(TaskItemKind.Calendar));

        Assert.Null(entry.Product);
    }

    /// <summary>
    /// An entry standing for a real shelf item has its answer there. Keeping a second copy here is
    /// keeping two answers, and the one nobody edits is the one that ends up wrong.
    /// </summary>
    [Fact]
    public void An_entry_that_already_stands_for_a_shelf_item_describes_nothing_of_its_own()
    {
        var entry = AnEntryAskingForFlour(
            new TaskItemSubject(TaskItemKind.Inventory, linkedInventoryItemId: Guid.NewGuid()));

        Assert.Null(entry.Product);
    }

    /// <summary>Which is also what happens the moment an entry is pointed at one - see TaskItem.PointAtShelfItem.</summary>
    [Fact]
    public void Being_pointed_at_a_shelf_item_hands_the_answer_over_to_it()
    {
        var entry = AnEntryAskingForFlour();
        var shelfItemId = Guid.NewGuid();

        entry.PointAtShelfItem(shelfItemId);

        Assert.Equal(shelfItemId, entry.LinkedInventoryItemId);
        Assert.Null(entry.Product);
    }

    /// <summary>An entry renamed to settle an id clash still asks for the same thing - see TaskItem.WithNewId.</summary>
    [Fact]
    public void A_renamed_entry_still_asks_for_the_same_thing()
        => Assert.Equal(Flour, AnEntryAskingForFlour().WithNewId().Product);

    /// <summary>
    /// A caller that said nothing about the product keeps what is stored - the rule that lets the phone
    /// and every older tab go on saving lists without emptying what was written on the web. See
    /// UpdateTaskListCommand.EntriesKeepingTheirProduct.
    /// </summary>
    [Fact]
    public void An_entry_that_says_nothing_keeps_what_it_already_asked_for()
    {
        var stored = AnEntryAskingForFlour();
        var incoming = TaskItem.Create(
            "Flour", dueDateUtc: null, isCompleted: false, subject: new TaskItemSubject(TaskItemKind.Inventory));

        incoming.KeepProductOf(stored);

        Assert.Equal(Flour, incoming.Product);
    }

    /// <summary>But not onto an entry that is no longer an inventory one: what it kept would not apply.</summary>
    [Fact]
    public void An_entry_that_stopped_being_an_inventory_one_keeps_nothing()
    {
        var stored = AnEntryAskingForFlour();
        var incoming = TaskItem.Create("Flour", dueDateUtc: null, isCompleted: false);

        incoming.KeepProductOf(stored);

        Assert.Null(incoming.Product);
    }

    [Fact]
    public void A_product_type_longer_than_the_column_is_refused_rather_than_cut()
    {
        var product = TaskItemProduct.Default with { ProductType = new string('a', StoredTextLimits.ProductType + 1) };

        Assert.Throws<InvalidRequestException>(() => TaskItem.Create(
            "Flour", dueDateUtc: null, isCompleted: false,
            subject: new TaskItemSubject(TaskItemKind.Inventory), product: product));
    }

    /// <summary>Nothing said about how it keeps is nothing said - not a promise nobody made.</summary>
    [Fact]
    public void An_entry_asks_for_no_expiry_warning_until_somebody_says_otherwise()
    {
        Assert.Equal(NotificationChannel.None, TaskItemProduct.Default.ExpiryNotificationChannel);
        Assert.Null(TaskItemProduct.Default.MinimumQuantity);
    }
}
