using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Core.Inventories;
using Orbit.Contracts.Inventories;
using Orbit.Mobile.Api;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Inventory;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Sync;
using Orbit.Contracts.Suggestions;
using Orbit.Core.Suggestions;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;
using Orbit.Mobile.Chat;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// An inventory item's own details. The phone created every item as one Piece of General with no minimum
/// and no expiry, and offered no way to change any of it - so four fields the browser sets were both
/// invisible and unreachable here.
/// </summary>
public sealed class InventoryDetailScreenTests
{
    /// <summary>
    /// A row is a batch rather than a product: two rows of one name are two deliveries of it, and when
    /// each arrived is what tells them apart. The browser's shelf says it; the phone showed three of the
    /// four things a shelf answers and left this one out.
    /// </summary>
    [Fact]
    public async Task A_row_says_when_its_batch_arrived()
    {
        using var context = new ScreenContext();
        var inventory = await context.PullInventoryAsync("Pantry", "Flour");

        var screen = await context.OpenAsync(inventory.LocalId);

        var row = Assert.Single(screen.Items);
        Assert.True(row.HasArrived);
        Assert.Contains("27", row.Arrived);
    }

    /// <summary>
    /// And says nothing about it for a row this phone queued and no server has accepted: nothing has
    /// taken delivery of it, so a date here would be the phone answering a question it was not asked.
    /// </summary>
    [Fact]
    public async Task A_row_no_server_has_taken_yet_says_nothing_about_arriving()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync("Pantry");
        var screen = await context.OpenAsync(inventory.LocalId);

        screen.NewItemName = "Flour";
        await screen.AddItemCommand.ExecuteAsync(null);

        Assert.False(Assert.Single(screen.Items).HasArrived);
    }

    /// <summary>
    /// A shelf opened from somewhere that meant one product - an errand naming it, or a search that
    /// found it - marks that row. Sixty rows and no sign of which one was meant is half an answer, and
    /// Orbit.Web's own shelf marks the row its ?highlight= names.
    /// </summary>
    [Fact]
    public async Task A_shelf_opened_for_one_product_marks_that_row()
    {
        using var context = new ScreenContext();
        var flour = Guid.NewGuid();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(Guid.NewGuid(), "Coffee", "", "", 2, null, nameof(InventoryUnit.Piece), null, "None"),
            new InventoryItemRequest(flour, "Flour", "", "", 1, null, nameof(InventoryUnit.Kilogram), null, "None"));

        var screen = await context.OpenAsync(inventory.LocalId, flour);

        var marked = Assert.Single(screen.Items, row => row.IsPointedAt);
        Assert.Equal("Flour", marked.Name);
        Assert.Equal(marked, screen.PointedAtRow);
        // A colour says nothing to somebody who cannot see it, so the row says it in words as well.
        Assert.NotEqual(marked.Name, marked.NameForAReader);
    }

    /// <summary>A shelf opened for its own sake marks nothing - there is nothing it was opened about.</summary>
    [Fact]
    public async Task A_shelf_opened_for_its_own_sake_marks_nothing()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(Guid.NewGuid(), "Coffee", "", "", 2, null, nameof(InventoryUnit.Piece), null, "None"));

        var screen = await context.OpenAsync(inventory.LocalId);

        Assert.All(screen.Items, row => Assert.False(row.IsPointedAt));
        Assert.Null(screen.PointedAtRow);
    }

    /// <summary>
    /// The mark outlives a filter, the way the browser's ?highlight= outlives one: narrowing the shelf
    /// and widening it again should find the row still marked, not quietly forgotten.
    /// </summary>
    [Fact]
    public async Task Narrowing_the_shelf_and_widening_it_again_keeps_the_mark()
    {
        using var context = new ScreenContext();
        var flour = Guid.NewGuid();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(Guid.NewGuid(), "Coffee", "", "", 2, null, nameof(InventoryUnit.Piece), null, "None"),
            new InventoryItemRequest(flour, "Flour", "", "", 1, null, nameof(InventoryUnit.Kilogram), null, "None"));
        var screen = await context.OpenAsync(inventory.LocalId, flour);

        screen.SearchedName = "coffee";
        Assert.Null(screen.PointedAtRow);

        screen.SearchedName = string.Empty;

        Assert.Equal("Flour", screen.PointedAtRow?.Name);
    }

    [Fact]
    public async Task An_items_kind_and_minimum_are_shown()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(Guid.NewGuid(), "Coffee", "Bag", "Kitchen", 2, 5, nameof(InventoryUnit.Piece), null, "None"));

        var screen = await context.OpenAsync(inventory.LocalId);

        var row = Assert.Single(screen.Items);
        Assert.Contains("Bag", row.Detail);
        Assert.Contains("Kitchen", row.Detail);
        Assert.Contains("5", row.Detail);
    }

    /// <summary>
    /// Orbit.Web offers names under all four fields; the phone only had the two item ones, so the
    /// inventory's own name was a place a reader could quietly make the same storage twice.
    /// </summary>
    [Fact]
    public async Task Inventory_names_already_in_use_are_offered_under_the_name()
    {
        using var context = new ScreenContext();
        context.SuggestionsServer.Names.Add(new NameSuggestionDto("Kitchen, upstairs", 0.4));
        var inventory = await context.AddInventoryAsync();
        var screen = await context.OpenAsync(inventory.LocalId);

        screen.Name = "Kitc";

        await WaitUntil(() => screen.InventoryNameSuggestions.Names.Count > 0);
        Assert.Equal(["Kitchen, upstairs"], screen.InventoryNameSuggestions.Names);
        Assert.Equal(nameof(NameSuggestionKind.InventoryName), context.SuggestionsServer.LastKind);
        Assert.Empty(screen.Suggestions.Names);
    }

    /// <summary>Opening an inventory must not warn that its own name duplicates itself.</summary>
    [Fact]
    public async Task Opening_a_inventory_does_not_call_its_own_name_a_duplicate()
    {
        using var context = new ScreenContext();
        context.SuggestionsServer.Names.Add(new NameSuggestionDto("Kitchen", 0.9));
        var inventory = await context.AddInventoryAsync();

        var screen = await context.OpenAsync(inventory.LocalId);

        await Task.Delay(SettleTime);
        Assert.Empty(screen.InventoryNameSuggestions.Names);
        Assert.Equal(string.Empty, screen.InventoryNameSuggestions.DuplicateWarning);
    }

    /// <summary>Comfortably past the 150ms the lookup waits for the typing to stop.</summary>
    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(600);

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 40 && !condition(); attempt++)
        {
            await Task.Delay(50);
        }

        Assert.True(condition(), "The suggestions never arrived.");
    }

    /// <summary>The whole reason to set a minimum, and the same test Orbit.Web's editor makes.</summary>
    [Fact]
    public async Task An_item_below_its_minimum_is_marked()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(Guid.NewGuid(), "Coffee", "Bag", "Kitchen", 2, 5, nameof(InventoryUnit.Piece), null, "None"),
            new InventoryItemRequest(Guid.NewGuid(), "Tea", "Bag", "Kitchen", 9, 5, nameof(InventoryUnit.Piece), null, "None"));

        var screen = await context.OpenAsync(inventory.LocalId);

        Assert.True(screen.Items.Single(row => row.Name == "Coffee").IsRunningLow);
        Assert.False(screen.Items.Single(row => row.Name == "Tea").IsRunningLow);
    }

    /// <summary>An item with no minimum is not running low - it has no line to be under.</summary>
    [Fact]
    public async Task An_item_with_no_minimum_is_never_running_low()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(Guid.NewGuid(), "Coffee", "Bag", "Kitchen", 0, null, nameof(InventoryUnit.Piece), null, "None"));

        var screen = await context.OpenAsync(inventory.LocalId);

        Assert.False(Assert.Single(screen.Items).IsRunningLow);
    }

    [Fact]
    public async Task Editing_an_item_keeps_what_was_typed()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(Guid.NewGuid(), "Coffee", "Piece", "General", 1, null, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(inventory.LocalId);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.ProductType = "Bag";
        screen.BeingEdited.Category = "Kitchen";
        screen.BeingEdited.MinimumQuantity = "5";
        // Asked as how long it keeps rather than the day it stops - see ExpiryPeriod.
        screen.BeingEdited.ChosenExpiryUnit = ExpiryUnitChoice.For(screen.BeingEdited.ExpiryUnits, ExpiryUnit.Weeks);
        screen.BeingEdited.ExpiresIn = "2";
        await screen.SaveItemCommand.ExecuteAsync(null);

        var row = Assert.Single(screen.Items);
        Assert.Equal("Bag", row.Item.ProductType);
        Assert.Equal("Kitchen", row.Item.Category);
        Assert.Equal(5, row.Item.MinimumQuantity);
        Assert.Equal(DateTime.Today.AddDays(14), row.Item.ExpiryDate!.Value.LocalDateTime.Date);
    }

    /// <summary>
    /// What the inventory holds, under its name - the same field a task list gained, and for the same
    /// reason: it is the rest of the sentence the name starts. A private one keeps none.
    /// </summary>
    [Fact]
    public async Task A_inventory_can_say_what_it_is_for()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync();
        var screen = await context.OpenAsync(inventory.LocalId);

        screen.Description = "Dry goods, and the freezer in the garage.";
        // <inheritdoc cref="TaskListDetailScreenTests" /> - leaving the box is what saves it.
        await screen.CommitDescriptionCommand.ExecuteAsync(null);

        Assert.Equal("Dry goods, and the freezer in the garage.", context.Stored().Description);
    }

    /// <summary>
    /// A different question from the minimum: that one asks when there is too little of something, this
    /// one asks for it every round however much there is - see InventoryItem.BelongsOnTheRestockList.
    /// Added to the web on 2026-09-01; the phone could neither see nor set it.
    /// </summary>
    [Fact]
    public async Task An_item_can_be_asked_for_every_round()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(Guid.NewGuid(), "Coffee", "Piece", "General", 1, null, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(inventory.LocalId);

        screen.EditItemCommand.Execute(screen.Items[0]);
        Assert.False(screen.BeingEdited!.IsCheckedRegularly);

        screen.BeingEdited.IsCheckedRegularly = true;
        await screen.SaveItemCommand.ExecuteAsync(null);

        Assert.True(Assert.Single(screen.Items).Item.IsCheckedRegularly);
    }

    /// <summary>
    /// Null on the way in means "not provided" and keeps what is stored, so a save from a screen that
    /// says nothing cannot turn the flag off. The phone therefore always says which it is - otherwise
    /// somebody switching it off here would have been ignored.
    /// </summary>
    [Fact]
    public async Task Turning_it_off_is_said_rather_than_left_unsaid()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(
                Guid.NewGuid(), "Coffee", "Piece", "General", 1, null, nameof(InventoryUnit.Piece), null, "None",
                IsCheckedRegularly: true));
        var screen = await context.OpenAsync(inventory.LocalId);

        screen.EditItemCommand.Execute(screen.Items[0]);
        Assert.True(screen.BeingEdited!.IsCheckedRegularly);

        screen.BeingEdited.IsCheckedRegularly = false;
        await screen.SaveItemCommand.ExecuteAsync(null);

        Assert.False(Assert.Single(screen.Items).Item.IsCheckedRegularly);
    }

    /// <summary>
    /// An amount of "2" says nothing about whether that is two bottles or two litres, which is why
    /// Orbit.Web writes the unit beside it. Pieces are left off: "2" of a thing already means two of
    /// them.
    /// </summary>
    [Fact]
    public async Task An_amount_is_said_in_what_it_is_counted_in()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(Guid.NewGuid(), "Milk", "Bottle", "Kitchen", 2, 5, nameof(InventoryUnit.Litre), null, "None"),
            new InventoryItemRequest(Guid.NewGuid(), "Cups", "Piece", "Kitchen", 4, null, nameof(InventoryUnit.Piece), null, "None"));

        var screen = await context.OpenAsync(inventory.LocalId);

        var milk = screen.Items.Single(row => row.Name == "Milk");
        Assert.Equal("2 l", milk.Amount);
        Assert.Contains("5 l", milk.Detail);
        Assert.Equal("4", screen.Items.Single(row => row.Name == "Cups").Amount);
    }

    /// <summary>
    /// A full inventory could only be read top to bottom on the phone. Narrowing shows fewer rows and
    /// changes nothing about what is stored - the save carries the whole list either way.
    /// </summary>
    [Fact]
    public async Task A_shelf_can_be_narrowed_to_one_kind_of_thing()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(Guid.NewGuid(), "Coffee", "Bag", "Kitchen", 2, null, nameof(InventoryUnit.Piece), null, "None"),
            new InventoryItemRequest(Guid.NewGuid(), "Soap", "Bar", "Bathroom", 1, null, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(inventory.LocalId);

        screen.ChosenProductType = "Bag";

        Assert.Equal("Coffee", Assert.Single(screen.Items).Name);
        Assert.True(screen.IsNarrowed);
        Assert.Contains("1", screen.FilterNote);
    }

    /// <summary>
    /// The whole shelf comes back, which is what stops somebody saving an inventory in the belief that
    /// the rows they cannot see are gone.
    /// </summary>
    [Fact]
    public async Task Narrowing_hides_rows_without_dropping_them()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(Guid.NewGuid(), "Coffee", "Bag", "Kitchen", 2, null, nameof(InventoryUnit.Piece), null, "None"),
            new InventoryItemRequest(Guid.NewGuid(), "Soap", "Bar", "Bathroom", 1, null, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(inventory.LocalId);
        screen.ChosenCategory = "Kitchen";

        await screen.RenameCommand.ExecuteAsync(null);
        screen.ShowEverythingCommand.Execute(null);

        Assert.Equal(2, screen.Items.Count);
        Assert.False(screen.IsNarrowed);
    }

    /// <summary>
    /// A new row is filed under nothing yet, so a filter left in place would hide it the moment it
    /// appeared - which reads as an Add button that does nothing.
    /// </summary>
    [Fact]
    public async Task Adding_an_item_steps_the_filter_aside()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(Guid.NewGuid(), "Coffee", "Bag", "Kitchen", 2, null, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(inventory.LocalId);
        screen.ChosenProductType = "Bag";

        screen.NewItemName = "Tea";
        await screen.AddItemCommand.ExecuteAsync(null);

        Assert.False(screen.IsNarrowed);
        Assert.Contains(screen.Items, row => row.Name == "Tea");
    }

    /// <summary>
    /// An item added on the phone arrives with no kind and no category, exactly as one added in a
    /// browser does. The phone used to write "Piece" and "General" instead - a unit's name in the field
    /// for a kind of thing, English on a shelf kept in any other language, and two words nobody typed
    /// showing up in the filters above and on the row itself. Found on a device.
    /// </summary>
    [Fact]
    public async Task An_item_added_here_is_filed_under_nothing_until_somebody_files_it()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync();
        var screen = await context.OpenAsync(inventory.LocalId);

        screen.NewItemName = "Flour";
        await screen.AddItemCommand.ExecuteAsync(null);

        var added = Assert.Single(screen.Items);
        Assert.Equal(string.Empty, added.Item.ProductType);
        Assert.Equal(string.Empty, added.Item.Category);

        // And nothing on the row about a kind it does not have.
        Assert.Equal(string.Empty, added.Detail);
    }

    /// <summary>
    /// One item with nothing filled in is nothing to file by, so neither picker is offered. The invented
    /// defaults made every inventory look like it had a type and a category worth filtering on.
    ///
    /// Searching by name is offered anyway, which is the one condition it does not share: a name is
    /// typed, and every item has one, including this one.
    /// </summary>
    [Fact]
    public async Task A_shelf_of_unfiled_items_offers_only_the_search()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync();
        var screen = await context.OpenAsync(inventory.LocalId);

        screen.NewItemName = "Flour";
        await screen.AddItemCommand.ExecuteAsync(null);

        Assert.False(screen.CanNarrowByProductType);
        Assert.False(screen.CanNarrowByCategory);
        Assert.True(screen.CanNarrow);
    }

    /// <summary>
    /// A long shelf could be narrowed to a type or a category, but finding one thing on it still meant
    /// reading it. Matched anywhere in the name and regardless of case, because a shelf holds "Flour,
    /// wheat" and "Wholemeal flour" and somebody typing "flour" means both.
    /// </summary>
    [Fact]
    public async Task A_shelf_can_be_searched_by_name()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(Guid.NewGuid(), "Flour, wheat", "Bag", "Kitchen", 2, null, nameof(InventoryUnit.Piece), null, "None"),
            new InventoryItemRequest(Guid.NewGuid(), "Wholemeal flour", "Bag", "Kitchen", 1, null, nameof(InventoryUnit.Piece), null, "None"),
            new InventoryItemRequest(Guid.NewGuid(), "Soap", "Bar", "Bathroom", 1, null, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(inventory.LocalId);

        screen.SearchedName = "flour";

        Assert.Equal(2, screen.Items.Count);
        Assert.DoesNotContain(screen.Items, row => row.Name == "Soap");
        Assert.True(screen.IsNarrowed);
    }

    /// <summary>
    /// The three narrow together, so a search inside a category is a search inside that category rather
    /// than a search that quietly threw the category away.
    /// </summary>
    [Fact]
    public async Task A_search_inside_a_category_stays_inside_it()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(Guid.NewGuid(), "Flour", "Bag", "Kitchen", 2, null, nameof(InventoryUnit.Piece), null, "None"),
            new InventoryItemRequest(Guid.NewGuid(), "Flour paste", "Tube", "Workshop", 1, null, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(inventory.LocalId);

        screen.ChosenCategory = "Kitchen";
        screen.SearchedName = "flour";

        Assert.Equal("Flour", Assert.Single(screen.Items).Name);
    }

    /// <summary>
    /// Searching hides rows from the screen and never from the inventory, the same as the two pickers -
    /// what stops somebody saving in the belief that the rows they cannot see are gone.
    /// </summary>
    [Fact]
    public async Task Showing_everything_clears_the_search_too()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(Guid.NewGuid(), "Flour", "Bag", "Kitchen", 2, null, nameof(InventoryUnit.Piece), null, "None"),
            new InventoryItemRequest(Guid.NewGuid(), "Soap", "Bar", "Bathroom", 1, null, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(inventory.LocalId);
        screen.SearchedName = "flour";

        screen.ShowEverythingCommand.Execute(null);

        Assert.Equal(string.Empty, screen.SearchedName);
        Assert.Equal(2, screen.Items.Count);
        Assert.False(screen.IsNarrowed);
    }

    /// <summary>
    /// A new row is named but filed under nothing, so a search left in place would hide it the moment it
    /// appeared - an Add button that reads as doing nothing, the same failure the pickers had.
    /// </summary>
    [Fact]
    public async Task Adding_an_item_steps_the_search_aside()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(Guid.NewGuid(), "Flour", "Bag", "Kitchen", 2, null, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(inventory.LocalId);
        screen.SearchedName = "flour";

        screen.NewItemName = "Tea";
        await screen.AddItemCommand.ExecuteAsync(null);

        Assert.False(screen.IsNarrowed);
        Assert.Contains(screen.Items, row => row.Name == "Tea");
    }

    /// <summary>
    /// Whitespace is not a search. Otherwise a stray space left in the box would announce a narrowed
    /// shelf that is not narrowed, and offer to show everything that is already shown.
    /// </summary>
    [Fact]
    public async Task A_box_holding_only_spaces_is_not_a_search()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(Guid.NewGuid(), "Flour", "Bag", "Kitchen", 2, null, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(inventory.LocalId);

        screen.SearchedName = "   ";

        Assert.False(screen.IsNarrowed);
        Assert.Single(screen.Items);
    }

    /// <summary>
    /// What a MAUI Picker does that no test did: emptying its ItemsSource clears its selection, and the
    /// binding writes that null back here. Every reload empties both pickers before refilling them, so
    /// the null arrived on the way to redrawing the shelf - and the filter, holding it, dereferenced it.
    /// Adding one item to an inventory was enough to kill the app.
    /// </summary>
    [Fact]
    public async Task A_picker_that_clears_its_own_selection_does_not_take_the_screen_down()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(Guid.NewGuid(), "Coffee", "Bag", "Kitchen", 2, null, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(inventory.LocalId);

        screen.ChosenCategory = null;
        screen.NewItemName = "Tea";
        await screen.AddItemCommand.ExecuteAsync(null);

        // Nothing narrowed by a selection nobody made: the whole shelf, the new row included.
        Assert.False(screen.IsNarrowed);
        Assert.Equal(2, screen.Items.Count);
    }

    /// <summary>
    /// A shelf is arranged, and the phone could only add to the end of one. Orbit.Web drags rows into
    /// place; the order an inventory is saved in is the order it is stored in, so the two arrive at the
    /// same shelf.
    /// </summary>
    [Fact]
    public async Task A_product_can_be_moved_up_the_shelf()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            Product("Coffee"), Product("Soap"), Product("Rice"));
        var screen = await context.OpenAsync(inventory.LocalId);

        await screen.MoveItemUpCommand.ExecuteAsync(screen.Items.Single(row => row.Name == "Rice"));

        Assert.Equal(["Coffee", "Rice", "Soap"], screen.Items.Select(row => row.Name));
    }

    [Fact]
    public async Task A_product_can_be_moved_down_the_shelf()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            Product("Coffee"), Product("Soap"), Product("Rice"));
        var screen = await context.OpenAsync(inventory.LocalId);

        await screen.MoveItemDownCommand.ExecuteAsync(screen.Items.Single(row => row.Name == "Coffee"));

        Assert.Equal(["Soap", "Coffee", "Rice"], screen.Items.Select(row => row.Name));
    }

    /// <summary>
    /// One place among what is shown, not among what is stored. A narrowed shelf hides rows, and
    /// swapping with a hidden neighbour would move the product without anything on screen changing -
    /// which reads as a button that does nothing.
    /// </summary>
    [Fact]
    public async Task Moving_a_product_on_a_narrowed_shelf_moves_it_past_what_is_shown()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            Product("Coffee", category: "Kitchen"),
            Product("Soap", category: "Bathroom"),
            Product("Rice", category: "Kitchen"));
        var screen = await context.OpenAsync(inventory.LocalId);
        screen.ChosenCategory = "Kitchen";

        await screen.MoveItemUpCommand.ExecuteAsync(screen.Items.Single(row => row.Name == "Rice"));

        // Visibly one place up among the kitchen rows...
        Assert.Equal(["Rice", "Coffee"], screen.Items.Select(row => row.Name));

        // ...and the hidden row stayed where it was rather than being carried along.
        screen.ShowEverythingCommand.Execute(null);
        Assert.Equal(["Rice", "Coffee", "Soap"], screen.Items.Select(row => row.Name));
    }

    /// <summary>The ends are where a shelf stops, not a failure - the top row has nowhere above it.</summary>
    [Fact]
    public async Task The_ends_of_the_shelf_hold()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(Product("Coffee"), Product("Soap"));
        var screen = await context.OpenAsync(inventory.LocalId);

        await screen.MoveItemUpCommand.ExecuteAsync(screen.Items[0]);
        await screen.MoveItemDownCommand.ExecuteAsync(screen.Items[1]);

        Assert.Equal(["Coffee", "Soap"], screen.Items.Select(row => row.Name));
    }

    /// <summary>
    /// A private shelf this device cannot open. Saving it would replace the sealed inventory with the
    /// empty one on screen - see the same guard on the task list.
    /// </summary>
    [Fact]
    public async Task A_private_inventory_this_device_cannot_open_is_read_only_here()
    {
        using var context = new ScreenContext();

        var screen = await context.OpenPrivateInventoryAsync();

        Assert.True(screen.IsReadOnly);
        Assert.False(screen.CanEdit);
        Assert.NotEmpty(screen.ReadOnlyReason);
    }

    [Fact]
    public async Task Making_a_inventory_private_seals_it_and_leaves_the_readable_columns_empty()
    {
        using var context = new ScreenContext(PrivateContent.HoldingAKeyFor(Owner));
        var inventory = await context.AddInventoryAsync(Product("Coffee"));
        var screen = await context.OpenAsync(inventory.LocalId);

        screen.IsPrivate = true;
        await screen.RenameCommand.ExecuteAsync(null);

        var stored = context.Stored();
        Assert.True(stored.IsPrivate);
        Assert.Equal(string.Empty, stored.Name);
        Assert.Empty(stored.Items);
        Assert.NotNull(stored.EncryptedContent);
    }

    [Fact]
    public async Task A_inventory_this_device_sealed_opens_again_with_its_shelf_back()
    {
        using var context = new ScreenContext(PrivateContent.HoldingAKeyFor(Owner));
        var inventory = await context.AddInventoryAsync(Product("Coffee"));
        var screen = await context.OpenAsync(inventory.LocalId);
        screen.IsPrivate = true;
        await screen.RenameCommand.ExecuteAsync(null);

        var reopened = await context.OpenAsync(inventory.LocalId);

        Assert.False(reopened.IsReadOnly);
        Assert.True(reopened.IsPrivate);
        Assert.Equal(["Coffee"], reopened.Items.Select(row => row.Name));
        Assert.False(reopened.Share.CanShare);
    }

    /// <inheritdoc cref="NoteDetailScreenTests"/>
    [Fact]
    public async Task Making_a_inventory_private_without_a_key_asks_for_it_rather_than_saving()
    {
        using var context = new ScreenContext(PrivateContent.SignedInWithoutAKey(Owner));
        var inventory = await context.AddInventoryAsync(Product("Coffee"));
        var screen = await context.OpenAsync(inventory.LocalId);

        screen.IsPrivate = true;
        await screen.RenameCommand.ExecuteAsync(null);

        Assert.Contains(nameof(IScreenNavigator.ShowChatKeyGate), context.Navigator.Destinations);
        Assert.False(context.Stored().IsPrivate);
    }

    /// <summary>Whoever is signed in - only its identity matters, as the key is kept per account.</summary>
    private static readonly Guid Owner = Guid.Parse("11111111-0000-4000-8000-000000000001");

    private static InventoryItemRequest Product(string name, string category = "General")
        => new(Guid.NewGuid(), name, "Bag", category, 1, null, nameof(InventoryUnit.Piece), null, "None");

    /// <summary>
    /// The unit is the phone's to set, not just to pass along - a shelf counted in kilograms is stocked
    /// from the phone as often as from the browser.
    /// </summary>
    [Fact]
    public async Task An_items_unit_can_be_changed()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(Guid.NewGuid(), "Flour", "Bag", "Kitchen", 2, null, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(inventory.LocalId);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.ChosenUnit = InventoryUnitChoice.For(
            screen.BeingEdited.Units, nameof(InventoryUnit.Kilogram));
        await screen.SaveItemCommand.ExecuteAsync(null);

        Assert.Equal(nameof(InventoryUnit.Kilogram), Assert.Single(screen.Items).Item.Unit);
    }

    /// <summary>
    /// An empty minimum box means no minimum, which is not a minimum of zero: zero would mark everything
    /// as adequately stocked forever, and is a different statement about the shelf.
    /// </summary>
    [Fact]
    public async Task Clearing_the_minimum_removes_it_rather_than_setting_zero()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(Guid.NewGuid(), "Coffee", "Bag", "Kitchen", 2, 5, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(inventory.LocalId);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.MinimumQuantity = string.Empty;
        await screen.SaveItemCommand.ExecuteAsync(null);

        Assert.Null(Assert.Single(screen.Items).Item.MinimumQuantity);
    }

    [Fact]
    public async Task Turning_the_expiry_off_removes_the_date()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(
                Guid.NewGuid(), "Milk", "Bottle", "Kitchen", 1, null, nameof(InventoryUnit.Piece),
                new DateTimeOffset(2027, 3, 1, 0, 0, 0, TimeSpan.Zero), "Push"));
        var screen = await context.OpenAsync(inventory.LocalId);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.ChosenExpiryUnit = ExpiryUnitChoice.For(screen.BeingEdited.ExpiryUnits, ExpiryUnit.None);
        await screen.SaveItemCommand.ExecuteAsync(null);

        Assert.Null(Assert.Single(screen.Items).Item.ExpiryDate);
    }

    /// <summary>Cancelling leaves the shelf as it was - the editor is a draft until it is saved.</summary>
    [Fact]
    public async Task Cancelling_changes_nothing()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync(
            new InventoryItemRequest(Guid.NewGuid(), "Coffee", "Piece", "General", 1, null, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(inventory.LocalId);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.ProductType = "Bag";
        screen.CancelItemEditCommand.Execute(null);

        Assert.Null(screen.BeingEdited);
        Assert.Equal("Piece", Assert.Single(screen.Items).Item.ProductType);
    }

    /// <summary>
    /// Orbit.Web has had both since the beginning; the phone showed an inventory's name without letting
    /// anybody change it, and offered no way to get rid of the inventory from anywhere at all.
    /// </summary>
    [Fact]
    public async Task A_inventory_can_be_renamed()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync("Kitchn");
        var screen = await context.OpenAsync(inventory.LocalId);

        screen.Name = "Kitchen";
        await screen.RenameCommand.ExecuteAsync(null);

        var reopened = await context.OpenAsync(inventory.LocalId);
        Assert.Equal("Kitchen", reopened.Name);
    }

    [Fact]
    public async Task A_inventory_can_be_deleted_and_the_screen_leaves()
    {
        using var context = new ScreenContext();
        var inventory = await context.AddInventoryAsync("Kitchen");
        var screen = await context.OpenAsync(inventory.LocalId);

        await screen.DeleteCommand.ExecuteAsync(null);

        Assert.Empty(await context.StoredInventoriesAsync());
        Assert.Contains(nameof(IScreenNavigator.ShowInventory), context.Navigator.Destinations);
    }

    private sealed class ScreenContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-27T10:00:00Z"));
        private readonly LocalInventoryRepository _inventories;
        private readonly InventorySynchronizer _synchronizer;
        private readonly PrivateContentSealer _privateContent;

        public ScreenContext(PrivateContentSealer? privateContent = null)
        {
            _privateContent = privateContent ?? PrivateContent.WithoutAKey();
            Server = new FakeInventoryServer(_clock);
            _inventories = new LocalInventoryRepository(_localStore, _clock, FixedNetworkStatus.Online, _privateContent);
            _synchronizer = new InventorySynchronizer(
                _localStore, new InventoryClient(Server.ToHttpClient()), _clock, new SyncGate(),
                NullLogger<InventorySynchronizer>.Instance);
        }

        public FakeInventoryServer Server { get; }

        public RecordingScreenNavigator Navigator { get; } = new();

        /// <summary>What this account has already named - see NameSuggestions. Empty unless a test fills it.</summary>
        public FakeSuggestionsServer SuggestionsServer { get; } = new();

        /// <summary>An inventory is created empty, so its items are put in by the same update a screen makes.</summary>
        public Task<LocalInventory> AddInventoryAsync(string name)
            => _inventories.CreateAsync(name);

        /// <summary>
        /// A shelf as it comes down from a server, which is the only way a batch's arrival is known -
        /// see LocalInventory.ItemArrivals.
        /// </summary>
        public async Task<LocalInventory> PullInventoryAsync(string name, params string[] productNames)
        {
            var remote = Server.AddInventory(name);
            foreach (var productName in productNames)
            {
                Server.AddItem(remote.Id, productName, quantity: 1);
            }

            await _synchronizer.SynchroniseAsync(CancellationToken.None);
            return (await _inventories.GetAllAsync()).Single(inventory => inventory.ServerId == remote.Id);
        }

        /// <summary>
        /// The one row as it really sits in the database, rather than as a read hands it back opened.
        /// </summary>
        public LocalInventory Stored()
        {
            using var dbContext = _localStore.CreateDbContext();
            return dbContext.Inventories.Single();
        }

        /// <summary>What the local store holds now - for the one test that expects it to hold nothing.</summary>
        public async Task<IReadOnlyList<LocalInventory>> StoredInventoriesAsync()
            => await _inventories.GetAllAsync();

        /// <summary>A shelf sealed with a key this phone has not got, as the sync would bring one down.</summary>
        public async Task<InventoryDetailViewModel> OpenPrivateInventoryAsync()
        {
            var inventory = await AddInventoryAsync();
            await using (var dbContext = _localStore.CreateDbContext())
            {
                dbContext.Inventories.Single().IsPrivate = true;
                await dbContext.SaveChangesAsync();
            }

            return await OpenAsync(inventory.LocalId);
        }

        public async Task<LocalInventory> AddInventoryAsync(params InventoryItemRequest[] items)
        {
            var inventory = await _inventories.CreateAsync("Kitchen");
            await _inventories.UpdateAsync(inventory.LocalId, new InventoryContent(inventory.Name, items));
            return inventory;
        }

        /// <summary>Whether the phone has a connection, which is what the offline refusal turns on.</summary>
        public FixedNetworkStatus Network { get; } = FixedNetworkStatus.Online;

        public Task<InventoryDetailViewModel> OpenAsync(Guid localId) => OpenAsync(localId, null);

        /// <param name="productId">Which row the shelf was opened for - see IScreenNavigator.ShowInventory.</param>
        public async Task<InventoryDetailViewModel> OpenAsync(Guid localId, Guid? productId)
        {
            var screen = new InventoryDetailViewModel(
                _inventories, _synchronizer, new Translations(new InMemoryLanguageStore()),
                ShareTestPanel.For(_localStore, new ChatRepository(_localStore, _clock)),
                Navigator,
                new InventoryClient(Server.ToHttpClient()), NothingIsBeingEdited(_clock), _privateContent,
                Suggestions.Offering(SuggestionsServer), Suggestions.Offering(SuggestionsServer), Network,
                new RestockListSettingsPanel(
                    new InventoryClient(Server.ToHttpClient()), new Translations(new InMemoryLanguageStore()),
                    new ConnectionRequirement(Network, new Translations(new InMemoryLanguageStore()))));

            screen.Open(localId, productId);
            await screen.LoadCommand.ExecuteAsync(null);
            return screen;
        }

        /// <summary>
        /// A lock over a fake server that answers every claim with "yours" - these tests are about the
        /// editor, and EditLockTests covers what happens when somebody else is in it.
        /// </summary>
        private static EditLock NothingIsBeingEdited(TimeProvider clock)
            => new(FixedNetworkStatus.Online, clock, new Translations(new InMemoryLanguageStore()));

        public void Dispose()
        {
            Server.Dispose();
            _localStore.Dispose();
        }
    }
}
