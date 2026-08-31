using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Inventory;
using Orbit.Core.Inventory;
using Orbit.Core.Tasks;
using Orbit.Mobile.Location;
using Orbit.Contracts.Tasks;
using Orbit.Mobile.Api;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Screens.Tasks;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Sync;
using Orbit.Contracts.Suggestions;
using Orbit.Core.Suggestions;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Chat;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The task-list screen, driven the way a reader drives it: type, add, tick.
///
/// These exist because a screen holds state of its own - what it last read - and that is where a bug got
/// through. Every other test here works one layer down, on stores and synchronisers, which were right;
/// what was wrong was the screen keeping a copy that the sync had already made stale.
/// </summary>
public sealed class TaskListDetailScreenTests
{
    [Fact]
    public async Task Ticking_an_entry_just_added_keeps_the_id_the_server_gave_it()
    {
        // The bug this stands for: an entry added here has no server id until the push comes back with
        // one. The screen read the list before syncing and never again, so the tick was built on the
        // copy that still had none - and the server made a second entry and dropped the first, cutting
        // loose an inventory item's restock task, a reminder's "already sent today" record, an overdue
        // notice. Everything below the screen was correct; only the screen was stale.
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Groceries");
        screen.NewItemDescription = "Buy milk";
        await screen.AddItemCommand.ExecuteAsync(null);

        var entryId = Assert.Single(context.Server.TaskLists.Single().Items).Id;
        await screen.ToggleItemCommand.ExecuteAsync(Assert.Single(screen.Items));

        var entry = Assert.Single(context.Server.TaskLists.Single().Items);
        Assert.Equal(entryId, entry.Id);
        Assert.True(entry.IsCompleted);
    }

    /// <summary>
    /// The bug this stands for: an entry added on the phone came out with both notification channels
    /// set to None, where Orbit.Web starts a new entry on Push. Nothing on either screen says a channel
    /// is off, so a task added on the phone went overdue in silence and read as push being broken.
    /// </summary>
    [Fact]
    public async Task An_entry_added_here_will_notify_when_it_goes_overdue()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Groceries");
        screen.NewItemDescription = "Buy milk";

        await screen.AddItemCommand.ExecuteAsync(null);

        var entry = Assert.Single(context.Server.TaskLists.Single().Items);
        Assert.Equal("Push", entry.OverdueNotificationChannel);
        Assert.Equal("Push", entry.DailyReminderNotificationChannel);
        Assert.False(entry.RemindDaily);
    }

    /// <summary>
    /// Orbit.Web offers names under all four fields; the phone only had the two item ones, so a title
    /// was the one place a reader could quietly make the same list twice.
    /// </summary>
    [Fact]
    public async Task Titles_already_in_use_are_offered_under_the_title()
    {
        using var context = new ScreenContext();
        context.SuggestionsServer.Names.Add(new NameSuggestionDto("Groceries, weekly", 0.4));
        var screen = context.OpenTaskList("Untitled");

        screen.Title = "Groc";

        await WaitUntil(() => screen.TitleSuggestions.Names.Count > 0);
        Assert.Equal(["Groceries, weekly"], screen.TitleSuggestions.Names);
        Assert.Equal(nameof(NameSuggestionKind.TaskListTitle), context.SuggestionsServer.LastKind);
    }

    /// <summary>
    /// Two fields, two strips: the title and the box an errand is written in are on screen together, so
    /// one shared set of names would put a title under the wrong box.
    /// </summary>
    [Fact]
    public async Task Typing_a_title_leaves_the_entry_box_below_it_alone()
    {
        using var context = new ScreenContext();
        context.SuggestionsServer.Names.Add(new NameSuggestionDto("Groceries, weekly", 0.4));
        var screen = context.OpenTaskList("Untitled");

        screen.Title = "Groc";

        await WaitUntil(() => screen.TitleSuggestions.Names.Count > 0);
        Assert.Empty(screen.Suggestions.Names);
    }

    /// <summary>Opening a list must not warn that its own title duplicates itself - see NameSuggestions.StartsAt.</summary>
    [Fact]
    public async Task Opening_a_list_does_not_call_its_own_title_a_duplicate()
    {
        using var context = new ScreenContext();
        context.SuggestionsServer.Names.Add(new NameSuggestionDto("Groceries", 0.9));

        var screen = context.OpenTaskList("Groceries");

        await Task.Delay(SettleTime);
        Assert.Empty(screen.TitleSuggestions.Names);
        Assert.Equal(string.Empty, screen.TitleSuggestions.DuplicateWarning);
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

    [Fact]
    public async Task An_entry_added_offline_reaches_the_server_with_one_id_once_the_connection_returns()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Groceries");
        await context.SynchroniseAsync();

        context.Server.IsUnreachable = true;
        screen.NewItemDescription = "Buy milk";
        await screen.AddItemCommand.ExecuteAsync(null);
        Assert.Equal("Saved on this phone - it will sync later", screen.Status);

        context.Server.IsUnreachable = false;
        await screen.ToggleItemCommand.ExecuteAsync(Assert.Single(screen.Items));
        await context.SynchroniseAsync();

        var entry = Assert.Single(context.Server.TaskLists.Single().Items);
        Assert.Equal("Buy milk", entry.Description);
        Assert.True(entry.IsCompleted);
    }

    [Fact]
    public async Task Removing_an_entry_leaves_the_others_with_the_ids_they_had()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Groceries");
        foreach (var description in new[] { "Buy milk", "Buy bread" })
        {
            screen.NewItemDescription = description;
            await screen.AddItemCommand.ExecuteAsync(null);
        }

        var breadId = context.Server.TaskLists.Single().Items.Single(item => item.Description == "Buy bread").Id;
        await screen.RemoveItemCommand.ExecuteAsync(screen.Items.Single(row => row.Description == "Buy milk"));

        var remaining = Assert.Single(context.Server.TaskLists.Single().Items);
        Assert.Equal(breadId, remaining.Id);
    }

    /// <summary>
    /// The rest of an entry, which the phone could neither see nor set: a due date, what happens when
    /// it passes, and whether it says something every day until the entry is done.
    /// </summary>
    [Fact]
    public async Task An_entrys_due_date_and_reminders_can_be_set()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Trip");
        screen.NewItemDescription = "pack";
        await screen.AddItemCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.HasDueDate = true;
        screen.BeingEdited.DueDate = new DateTime(2027, 3, 1);
        screen.BeingEdited.OverdueNotificationChannel = "Push";
        screen.BeingEdited.RemindDaily = true;
        await screen.SaveItemCommand.ExecuteAsync(null);

        var item = Assert.Single(screen.Items).Item;
        Assert.Equal(new DateTime(2027, 3, 1), item.DueDateUtc!.Value.LocalDateTime.Date);
        Assert.Equal("Push", item.OverdueNotificationChannel);
        Assert.True(item.RemindDaily);
    }

    /// <summary>
    /// An entry can be somewhere to be rather than something to fetch - see TaskItemKind. The phone
    /// carried the kind and the place through every save but could set neither, so a day's plan made
    /// here was all errands.
    /// </summary>
    [Fact]
    public async Task An_entry_can_be_made_an_appointment_with_a_place()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Saturday");
        screen.NewItemDescription = "dentist";
        await screen.AddItemCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Kind = nameof(TaskItemKind.Calendar);
        screen.BeingEdited.Location = "12 Mill Lane";
        await screen.SaveItemCommand.ExecuteAsync(null);

        var item = Assert.Single(screen.Items).Item;
        Assert.Equal(nameof(TaskItemKind.Calendar), item.Kind);
        Assert.Equal("12 Mill Lane", item.Location);
    }

    /// <summary>
    /// A Calendar entry is the appointment rather than a pointer at one: saving it is what puts it in
    /// the calendar, and the entry comes back carrying the event's id. This replaces the picker of
    /// events made elsewhere, which is what Orbit.Web dropped when it made the entry the event.
    /// </summary>
    [Fact]
    public async Task Saving_a_calendar_entry_puts_its_appointment_in_the_calendar()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Saturday");
        screen.NewItemDescription = "dentist";
        await screen.AddItemCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Kind = nameof(TaskItemKind.Calendar);
        screen.BeingEdited.Event.StartDate = new DateTime(2026, 9, 3);
        screen.BeingEdited.Event.StartTime = new TimeSpan(14, 30, 0);
        screen.BeingEdited.Event.EndDate = new DateTime(2026, 9, 3);
        screen.BeingEdited.Event.EndTime = new TimeSpan(15, 0, 0);
        await screen.SaveItemCommand.ExecuteAsync(null);

        var appointment = Assert.Single(context.CalendarServer.Events);
        // Its own words are the appointment's title: an entry and its appointment are one thing.
        Assert.Equal("dentist", appointment.Details.Title);
        Assert.Equal(appointment.Id, Assert.Single(screen.Items).Item.LinkedCalendarEventId);
    }

    /// <summary>
    /// Where a calendar entry happens stays on the entry, not on its appointment: the calendar's own
    /// location is coordinates first and an entry carries only a name, so the two are different fields.
    /// Orbit.Web leaves the name on the entry for the same reason.
    /// </summary>
    [Fact]
    public async Task A_calendar_entry_keeps_the_place_it_happens()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Saturday");
        screen.NewItemDescription = "dentist";
        await screen.AddItemCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Kind = nameof(TaskItemKind.Calendar);
        Assert.True(screen.BeingEdited.CanSayWhereItHappens);
        screen.BeingEdited.Location = "12 Mill Lane";
        await screen.SaveItemCommand.ExecuteAsync(null);

        Assert.Equal("12 Mill Lane", Assert.Single(screen.Items).Item.Location);
        Assert.Null(Assert.Single(context.CalendarServer.Events).Details.Location);
    }

    /// <summary>
    /// The one step on this screen that is not offline-capable, and it says so rather than saving an
    /// entry that points at an appointment nobody made.
    /// </summary>
    [Fact]
    public async Task An_appointment_that_cannot_be_written_stops_the_entry_being_saved()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Saturday");
        screen.NewItemDescription = "dentist";
        await screen.AddItemCommand.ExecuteAsync(null);
        context.CalendarServer.IsUnreachable = true;

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Kind = nameof(TaskItemKind.Calendar);
        await screen.SaveItemCommand.ExecuteAsync(null);

        // Still open, with what was typed - nothing is lost by waiting for a connection.
        Assert.NotNull(screen.BeingEdited);
        Assert.Contains("calendar", screen.Status);
        Assert.Null(Assert.Single(screen.Items).Item.LinkedCalendarEventId);
    }

    /// <summary>
    /// Opening an entry that already has an appointment fills the form from it, rather than showing an
    /// empty one that would overwrite the appointment on the next save.
    /// </summary>
    [Fact]
    public async Task Opening_an_entry_shows_the_appointment_it_already_has()
    {
        using var context = new ScreenContext();
        var startsAt = new DateTimeOffset(2026, 9, 3, 12, 30, 0, TimeSpan.Zero);
        var eventId = await context.AddCalendarEventAsync("Checkup", "12 Mill Lane", startsAt);
        var screen = context.OpenTaskList("Saturday");
        await context.AddCalendarEntryAsync(screen, "dentist", eventId);

        screen.EditItemCommand.Execute(screen.Items[0]);

        var form = screen.BeingEdited!.Event;
        Assert.Equal("Bring the letter", form.Description);
        Assert.Equal(startsAt.ToLocalTime().Date, form.StartDate);
        Assert.Equal(startsAt.ToLocalTime().TimeOfDay, form.StartTime);
        Assert.Equal(30, form.ChosenReminder?.MinutesBefore);
    }

    /// <summary>
    /// The whole point of giving these entries a kind and a link: the row already knows which product it
    /// means, so correcting an amount should not mean leaving the list and finding the warehouse again.
    /// Orbit.Web puts the same fields on its task editor.
    /// </summary>
    [Fact]
    public async Task An_errand_opens_the_product_it_is_about()
    {
        using var context = new ScreenContext();
        var shelf = await context.AddShelfProductAsync("Kitchen", "Coffee", quantity: 2);
        var screen = context.OpenTaskList("Saturday");
        await context.AddErrandAsync(screen, "Buy coffee", shelf.ProductId);

        screen.EditItemCommand.Execute(screen.Items[0]);

        var editor = screen.BeingEdited!;
        Assert.True(editor.IsShelfEntry);
        Assert.Equal("Coffee", editor.Shelf!.Product.Name);
        Assert.Contains("Kitchen", editor.WhereTheProductLives);
    }

    /// <summary>Saving the entry saves the shelf, which is what the line above the fields promises.</summary>
    [Fact]
    public async Task Correcting_a_product_from_an_errand_writes_it_back_to_the_warehouse()
    {
        using var context = new ScreenContext();
        var shelf = await context.AddShelfProductAsync("Kitchen", "Coffee", quantity: 2);
        var screen = context.OpenTaskList("Saturday");
        await context.AddErrandAsync(screen, "Buy coffee", shelf.ProductId);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Shelf!.Product.Quantity = "7";
        screen.BeingEdited.Shelf.Product.Name = "Coffee, ground";
        await screen.SaveItemCommand.ExecuteAsync(null);

        var stored = await context.Shelves.FindAsync(shelf.WarehouseLocalId);
        var product = Assert.Single(stored!.Items);
        Assert.Equal(7, product.Quantity);
        Assert.Equal("Coffee, ground", product.Name);
    }

    /// <summary>
    /// The correction has to leave the phone here, not whenever somebody next opens the warehouse: this
    /// screen is the only thing that knows the shelf was touched, and a restock list rebuilt before the
    /// new amount arrives would be rebuilt from the old one.
    /// </summary>
    [Fact]
    public async Task Correcting_a_product_from_an_errand_sends_it_to_the_server()
    {
        using var context = new ScreenContext();
        var shelf = await context.AddShelfProductAsync("Kitchen", "Coffee", quantity: 2);
        await context.ShelfSynchronizer.SynchroniseAsync();
        var screen = context.OpenTaskList("Saturday");
        await context.AddErrandAsync(screen, "Buy coffee", shelf.ProductId);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Shelf!.Product.Quantity = "7";
        await screen.SaveItemCommand.ExecuteAsync(null);

        var onTheServer = Assert.Single(context.Warehouses.Warehouses);
        var product = Assert.Single(context.Warehouses.ItemsIn(onTheServer.Id));
        Assert.Equal(7, product.Quantity);
    }

    /// <summary>
    /// An errand whose product this phone has not got - a warehouse no longer shared, or one not synced
    /// yet - still opens, with the shelf half missing rather than an empty form that looks broken.
    /// </summary>
    [Fact]
    public async Task An_errand_with_no_product_behind_it_says_so_rather_than_showing_an_empty_form()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Saturday");
        await context.AddErrandAsync(screen, "Buy coffee", Guid.NewGuid());

        screen.EditItemCommand.Execute(screen.Items[0]);

        Assert.False(screen.BeingEdited!.IsShelfEntry);
        Assert.True(screen.BeingEdited.HasNoProductToEdit);
        Assert.NotEmpty(screen.BeingEdited.NoProductMessage);
    }

    /// <summary>An appointment that ends before it starts is a typo, and is refused rather than saved.</summary>
    [Fact]
    public async Task An_appointment_that_ends_before_it_starts_cannot_be_saved()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Saturday");
        screen.NewItemDescription = "dentist";
        await screen.AddItemCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Kind = nameof(TaskItemKind.Calendar);
        screen.BeingEdited.Event.StartDate = new DateTime(2026, 9, 3);
        screen.BeingEdited.Event.EndDate = new DateTime(2026, 9, 1);

        Assert.False(screen.BeingEdited.CanSave);
        Assert.NotNull(screen.BeingEdited.WhatIsMissing);
        Assert.Empty(context.CalendarServer.Events);
    }

    /// <summary>
    /// Pointing at a place is the other way to say where something happens - the one that works when
    /// nobody knows what the street is called. The map opens where the box already pointed.
    /// </summary>
    [Fact]
    public async Task A_place_can_be_pointed_at_on_the_map()
    {
        using var context = new ScreenContext();
        context.PlacePicker.Result = PickedPlace.Chosen("12 Mill Lane");
        var screen = context.OpenTaskList("Saturday");
        screen.NewItemDescription = "dentist";
        await screen.AddItemCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Kind = nameof(TaskItemKind.Calendar);
        screen.BeingEdited.Location = "Mill Lane";
        await screen.ShowMapCommand.ExecuteAsync(null);

        Assert.Equal("Mill Lane", context.PlacePicker.StartedAt);
        Assert.Equal("12 Mill Lane", screen.BeingEdited.Location);
    }

    /// <summary>
    /// Backing out of the map writes nothing back: a stray tap must not rewrite an address somebody
    /// typed, which is the whole reason the map asks before answering.
    /// </summary>
    [Fact]
    public async Task Backing_out_of_the_map_keeps_what_was_typed()
    {
        using var context = new ScreenContext();
        context.PlacePicker.Result = PickedPlace.Cancelled;
        var screen = context.OpenTaskList("Saturday");
        screen.NewItemDescription = "dentist";
        await screen.AddItemCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Kind = nameof(TaskItemKind.Calendar);
        screen.BeingEdited.Location = "Mill Lane";
        await screen.ShowMapCommand.ExecuteAsync(null);

        Assert.Equal("Mill Lane", screen.BeingEdited.Location);
    }

    /// <summary>
    /// An errand is not somewhere to be, and an entry tied to an event has its place decided for it -
    /// so neither has a map to open. Offering one would be offering to overwrite nothing.
    /// </summary>
    [Fact]
    public async Task An_errand_has_no_map_to_open()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Saturday");
        screen.NewItemDescription = "buy milk";
        await screen.AddItemCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items[0]);
        await screen.ShowMapCommand.ExecuteAsync(null);

        Assert.Equal(0, context.PlacePicker.PickCount);
    }

    /// <summary>
    /// Only a calendar entry keeps its appointment, so one turned back into an errand lets go of it.
    /// The same rule Orbit.Web's editor applies. The appointment itself stays in the calendar - what
    /// the reader does with it there is their business, not this screen's.
    /// </summary>
    [Fact]
    public async Task An_entry_turned_back_into_an_errand_is_tied_to_nothing()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Saturday");
        screen.NewItemDescription = "dentist";
        await screen.AddItemCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Kind = nameof(TaskItemKind.Calendar);
        await screen.SaveItemCommand.ExecuteAsync(null);
        Assert.NotNull(Assert.Single(screen.Items).Item.LinkedCalendarEventId);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Kind = nameof(TaskItemKind.Checklist);
        await screen.SaveItemCommand.ExecuteAsync(null);

        Assert.Null(Assert.Single(screen.Items).Item.LinkedCalendarEventId);
    }

    /// <summary>
    /// Everything the editor does not show travels through untouched. An entry linked to an inventory
    /// item's restock task must come back linked, or the shelf loses its reminder.
    /// </summary>
    [Fact]
    public async Task Editing_an_entry_keeps_what_the_editor_does_not_show()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Trip");
        screen.NewItemDescription = "pack";
        await screen.AddItemCommand.ExecuteAsync(null);
        await screen.ToggleItemCommand.ExecuteAsync(screen.Items[0]);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Description = "pack properly";
        await screen.SaveItemCommand.ExecuteAsync(null);

        var item = Assert.Single(screen.Items).Item;
        Assert.Equal("pack properly", item.Description);
        Assert.True(item.IsCompleted);
    }

    /// <summary>A finished entry cannot be late any more, whatever its date says.</summary>
    [Fact]
    public async Task A_completed_entry_is_never_overdue()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Trip");
        screen.NewItemDescription = "pack";
        await screen.AddItemCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.HasDueDate = true;
        screen.BeingEdited.DueDate = new DateTime(2020, 1, 1);
        await screen.SaveItemCommand.ExecuteAsync(null);
        Assert.True(screen.Items[0].IsOverdue);

        await screen.ToggleItemCommand.ExecuteAsync(screen.Items[0]);

        Assert.False(screen.Items[0].IsOverdue);
    }

    /// <summary>
    /// Orbit.Web has a "Group list" checkbox; the phone had no way to set it, so a list made here
    /// could never be one - and the stock check, which only a group list is asked, was unreachable.
    /// </summary>
    [Fact]
    public async Task A_list_can_be_made_a_group_list()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Trip");

        Assert.False(screen.IsGroup);
        screen.IsGroup = true;
        // The switch starts the save rather than awaiting it, so the test waits on what it started -
        // asserting straight afterwards would be racing the write it is meant to be checking.
        await screen.SaveListCommand.ExecutionTask!;
        await context.SynchroniseAsync();

        Assert.Contains(context.Server.TaskLists, list => list.Title == "Trip" && list.IsGroup);
    }

    /// <summary>
    /// Orbit.Web's task editor has a Title field. This screen showed the title and would not let
    /// anybody change it - so a list named wrongly stayed named wrongly.
    /// </summary>
    [Fact]
    public async Task A_list_can_be_renamed()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Toady");

        screen.Title = "Today";
        await screen.SaveListCommand.ExecuteAsync(null);
        await context.SynchroniseAsync();

        Assert.Contains("Today", context.Server.TaskLists.Select(list => list.Title));
    }

    /// <summary>
    /// Moving an entry to another list, which the phone could not do at all. It is a change to two
    /// lists rather than to the entry, so it happens on choosing rather than on the form's Save - the
    /// same as Orbit.Web's editor.
    /// </summary>
    [Fact]
    public async Task An_entry_can_be_moved_to_another_list()
    {
        using var context = new ScreenContext();
        context.OpenTaskList("Later");
        var screen = context.OpenTaskList("Today");
        screen.NewItemDescription = "Call the plumber";
        await screen.AddItemCommand.ExecuteAsync(null);
        await context.SynchroniseAsync();
        await screen.LoadCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items.Single());
        var later = screen.MoveTargets.Single(target => target.Name == "Later");
        await screen.MoveItemCommand.ExecuteAsync(later);

        Assert.Empty(screen.Items);
        Assert.Contains("Later", screen.Status);
        Assert.Contains(
            "Call the plumber",
            context.Server.ItemsIn(later.ServerId!.Value).Select(item => item.Description));
    }

    /// <summary>
    /// A group list gathers other lists, and it gathers them through its entries pointing at them.
    /// The phone could turn "group list" on and point nothing anywhere, so a group made here gathered
    /// nothing at all - half a feature, which is worse than none. Orbit.Web has offered the picker all
    /// along.
    /// </summary>
    [Fact]
    public async Task An_entry_can_be_made_to_stand_for_another_list()
    {
        using var context = new ScreenContext();
        context.OpenTaskList("Shopping");
        var screen = context.OpenTaskList("This week");
        screen.NewItemDescription = "The shopping";
        await screen.AddItemCommand.ExecuteAsync(null);
        await context.SynchroniseAsync();
        await screen.LoadCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items.Single());
        screen.BeingEdited!.ChosenLinkedTaskList =
            screen.BeingEdited.LinkableTaskLists.Single(choice => choice.Name == "Shopping");
        await screen.SaveItemCommand.ExecuteAsync(null);
        await context.SynchroniseAsync();

        var shopping = context.Server.TaskLists.Single(list => list.Title == "Shopping");
        var thisWeek = context.Server.TaskLists.Single(list => list.Title == "This week");
        Assert.Equal(shopping.Id, Assert.Single(thisWeek.Items).LinkedTaskListId);
    }

    /// <summary>
    /// And can stop standing for it. Pointing at nothing is what most entries do, so the picker offers
    /// it rather than making "linked" a one-way door.
    /// </summary>
    [Fact]
    public async Task An_entry_can_stop_standing_for_a_list()
    {
        using var context = new ScreenContext();
        context.OpenTaskList("Shopping");
        var screen = context.OpenTaskList("This week");
        screen.NewItemDescription = "The shopping";
        await screen.AddItemCommand.ExecuteAsync(null);
        await context.SynchroniseAsync();
        await screen.LoadCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items.Single());
        screen.BeingEdited!.ChosenLinkedTaskList =
            screen.BeingEdited.LinkableTaskLists.Single(choice => choice.Name == "Shopping");
        await screen.SaveItemCommand.ExecuteAsync(null);

        await screen.LoadCommand.ExecuteAsync(null);
        screen.EditItemCommand.Execute(screen.Items.Single());
        Assert.Equal("Shopping", screen.BeingEdited!.ChosenLinkedTaskList?.Name);

        screen.BeingEdited.ChosenLinkedTaskList =
            screen.BeingEdited.LinkableTaskLists.Single(choice => choice.ServerId is null);
        await screen.SaveItemCommand.ExecuteAsync(null);
        await context.SynchroniseAsync();

        var thisWeek = context.Server.TaskLists.Single(list => list.Title == "This week");
        Assert.Null(Assert.Single(thisWeek.Items).LinkedTaskListId);
    }

    /// <summary>One list is nothing to point at, so the picker is not offered at all.</summary>
    [Fact]
    public async Task With_no_other_list_there_is_nothing_to_point_at()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("This week");
        screen.NewItemDescription = "The shopping";
        await screen.AddItemCommand.ExecuteAsync(null);
        await context.SynchroniseAsync();
        await screen.LoadCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items.Single());

        Assert.False(screen.BeingEdited!.CanBeLinked);
    }

    /// <summary>The list being looked at is not somewhere its own entries can go.</summary>
    [Fact]
    public async Task The_list_being_looked_at_is_not_one_of_the_places_to_move_to()
    {
        using var context = new ScreenContext();
        context.OpenTaskList("Later");
        var screen = context.OpenTaskList("Today");
        await context.SynchroniseAsync();
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.DoesNotContain(screen.MoveTargets, target => target.Name == "Today");
        Assert.Contains(screen.MoveTargets, target => target.Name == "Later");
    }

    /// <summary>
    /// An entry added on this phone has no id the server would recognise until it syncs, and offline
    /// there is nobody to do the moving. Neither is an error worth showing - the choice just isn't there.
    /// </summary>
    [Fact]
    public async Task An_entry_the_server_has_never_seen_cannot_be_moved()
    {
        using var context = new ScreenContext();
        context.OpenTaskList("Later");
        var screen = context.OpenTaskList("Today");
        await context.SynchroniseAsync();
        await screen.LoadCommand.ExecuteAsync(null);

        screen.NewItemDescription = "Call the plumber";
        context.Server.IsUnreachable = true;
        await screen.AddItemCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items.Single());

        Assert.False(screen.CanMoveItem);
    }


    /// <summary>
    /// Opening a warehouse's restock list settles what is already crossed off on it - each finished
    /// errand fills its shelf item and leaves. Orbit.Web asks for this on opening; a phone that did not
    /// would leave the same list behaving differently depending on which client last looked at it, and
    /// the shelf never topped up.
    /// </summary>
    [Fact]
    public async Task Opening_a_restock_list_settles_the_errands_already_crossed_off()
    {
        using var context = new ScreenContext();

        await context.OpenManagedRestockListAsync();

        Assert.Equal(1, context.Server.RestockingsSettled);
    }

    /// <summary>
    /// Nothing to settle on a list no warehouse tracks, so nothing is asked - the title is the only way
    /// to tell, and it is the same test Orbit.Web applies before making the call.
    /// </summary>
    [Fact]
    public async Task Opening_an_ordinary_list_asks_for_no_settlement()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Shopping");
        screen.NewItemDescription = "Buy flour";
        await screen.AddItemCommand.ExecuteAsync(null);

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Equal(0, context.Server.RestockingsSettled);
    }

    /// <summary>
    /// A settle moves entries off the list on the server, so what is on screen afterwards has to come
    /// back from there rather than from what was drawn before the call.
    /// </summary>
    [Fact]
    public async Task A_settle_that_moved_something_reads_the_list_back()
    {
        using var context = new ScreenContext();
        context.Server.SettledCount = 2;

        var screen = await context.OpenManagedRestockListAsync();

        Assert.Equal(1, context.Server.RestockingsSettled);
        Assert.Equal(["Buy flour"], screen.Items.Select(row => row.Description));
    }

    /// <summary>
    /// Crossing off "Update stock levels" while errands are still open is either the end of a round of
    /// restocking or a tick on the standing reminder, and only the reader knows which. Orbit.Web asks in
    /// the browser's confirm box; the phone asks in place, having nowhere to put a dialog.
    /// </summary>
    [Fact]
    public async Task Ticking_the_stock_reminder_with_errands_still_open_asks_first()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenRestockRoundAsync();

        await screen.ToggleItemCommand.ExecuteAsync(context.StockReminderIn(screen));

        Assert.True(screen.IsAskingToFinishRestocking);
        Assert.Equal(0, context.Server.RestockingsFinished);
        // Nothing is crossed off until the question has an answer.
        Assert.All(context.Server.TaskLists.Single().Items, item => Assert.False(item.IsCompleted));
    }

    [Fact]
    public async Task Answering_yes_brings_the_whole_warehouse_up_to_its_minimum()
    {
        using var context = new ScreenContext();
        context.Server.ToppedUpCount = 4;
        var screen = await context.OpenRestockRoundAsync();
        await screen.ToggleItemCommand.ExecuteAsync(context.StockReminderIn(screen));

        await screen.FinishRestockingCommand.ExecuteAsync(null);

        Assert.Equal(1, context.Server.RestockingsFinished);
        Assert.False(screen.IsAskingToFinishRestocking);
        Assert.Contains("4", screen.Status);
    }

    /// <summary>
    /// "No" is not a cancel: the reader did ask for that tick, and only the claim about the whole
    /// warehouse was declined.
    /// </summary>
    [Fact]
    public async Task Answering_no_crosses_off_that_entry_and_leaves_the_shelf_alone()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenRestockRoundAsync();
        await screen.ToggleItemCommand.ExecuteAsync(context.StockReminderIn(screen));

        await screen.TickOnlyThisCommand.ExecuteAsync(null);

        Assert.Equal(0, context.Server.RestockingsFinished);
        Assert.False(screen.IsAskingToFinishRestocking);
        var items = context.Server.TaskLists.Single().Items;
        Assert.True(items.Single(item => item.Description == RestockTaskNaming.UpdateStockReminderDescription).IsCompleted);
        Assert.False(items.Single(item => item.Description == "Buy flour").IsCompleted);
    }

    /// <summary>
    /// With nothing else outstanding there is no round to close early - the reminder is just an entry,
    /// and is ticked like one. Orbit.Web draws the line in the same place.
    /// </summary>
    [Fact]
    public async Task Ticking_the_stock_reminder_on_its_own_does_not_ask()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenRestockRoundAsync();
        await screen.ToggleItemCommand.ExecuteAsync(screen.Items.Single(row => row.Description == "Buy flour"));

        await screen.ToggleItemCommand.ExecuteAsync(context.StockReminderIn(screen));

        Assert.False(screen.IsAskingToFinishRestocking);
        Assert.Equal(0, context.Server.RestockingsFinished);
        Assert.All(context.Server.TaskLists.Single().Items, item => Assert.True(item.IsCompleted));
    }

    /// <summary>
    /// A checklist is read in order - first this, then that - and the phone could only add to the end
    /// of one, so an entry put down out of turn stayed out of turn for good. Orbit.Web has dragged them
    /// into place all along.
    /// </summary>
    [Fact]
    public async Task An_entry_can_be_moved_up_the_list()
    {
        using var context = new ScreenContext();
        var screen = await context.WithEntriesAsync("Wake up", "Coffee", "Leave");

        await screen.MoveItemUpCommand.ExecuteAsync(screen.Items.Single(row => row.Description == "Leave"));

        Assert.Equal(["Wake up", "Leave", "Coffee"], screen.Items.Select(row => row.Description));
    }

    [Fact]
    public async Task An_entry_can_be_moved_down_the_list()
    {
        using var context = new ScreenContext();
        var screen = await context.WithEntriesAsync("Wake up", "Coffee", "Leave");

        await screen.MoveItemDownCommand.ExecuteAsync(screen.Items.Single(row => row.Description == "Wake up"));

        Assert.Equal(["Coffee", "Wake up", "Leave"], screen.Items.Select(row => row.Description));
    }

    /// <summary>
    /// The order has to reach the server, or it is an arrangement that survives until the next device
    /// reads the list - the entries are stored in the order they are sent, one position each.
    /// </summary>
    [Fact]
    public async Task The_new_order_is_what_the_server_holds()
    {
        using var context = new ScreenContext();
        var screen = await context.WithEntriesAsync("Wake up", "Coffee", "Leave");

        await screen.MoveItemUpCommand.ExecuteAsync(screen.Items.Single(row => row.Description == "Leave"));

        Assert.Equal(
            ["Wake up", "Leave", "Coffee"],
            context.Server.TaskLists.Single().Items.Select(item => item.Description));
    }

    /// <summary>The ends are where a list stops, not a failure - the first entry has nowhere above it.</summary>
    [Fact]
    public async Task The_ends_of_the_list_hold()
    {
        using var context = new ScreenContext();
        var screen = await context.WithEntriesAsync("Wake up", "Coffee");

        await screen.MoveItemUpCommand.ExecuteAsync(screen.Items[0]);
        await screen.MoveItemDownCommand.ExecuteAsync(screen.Items[1]);

        Assert.Equal(["Wake up", "Coffee"], screen.Items.Select(row => row.Description));
    }

    /// <summary>
    /// How much a list matters is one of the five orders the phone sorts by, and it could neither show
    /// one nor set one: the sort was by something invisible, and a browser was the only way to change
    /// it. Orbit.Web's task editor has had the same three choices all along.
    /// </summary>
    [Fact]
    public async Task How_much_a_list_matters_can_be_set_here()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Move house");

        screen.ChosenPriority = screen.Priorities.Single(choice => choice.Value == "High");
        // The save the choice started, rather than a second one: setting it is what does the writing.
        await screen.SaveListCommand.ExecutionTask!;
        await context.SynchroniseAsync();

        Assert.Equal("High", context.Server.TaskLists.Single().Priority);
    }

    /// <summary>Set as soon as it is chosen, the way making a list a group list is.</summary>
    [Fact]
    public async Task Choosing_a_priority_is_saved_without_anything_else_being_pressed()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Move house");

        screen.ChosenPriority = screen.Priorities.Single(choice => choice.Value == "Low");
        await screen.SaveListCommand.ExecutionTask!;

        var stored = await context.FindAsync(screen);
        Assert.Equal("Low", stored.Priority);
    }

    /// <summary>
    /// A save writes the whole list, so a priority left out of it would go back to Normal every time
    /// somebody renamed the list from a phone - the mistake TaskList.Update's own comment records.
    /// </summary>
    [Fact]
    public async Task Renaming_a_list_leaves_its_priority_alone()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Move house");
        screen.ChosenPriority = screen.Priorities.Single(choice => choice.Value == "High");
        await screen.SaveListCommand.ExecutionTask!;

        screen.Title = "Move house, finally";
        await screen.SaveListCommand.ExecuteAsync(null);

        var stored = await context.FindAsync(screen);
        Assert.Equal("High", stored.Priority);
        Assert.Equal("Move house, finally", stored.Title);
    }

    /// <summary>What is stored is what the picker opens on, or the reader cannot see what they set.</summary>
    [Fact]
    public async Task The_picker_opens_on_what_the_list_already_is()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Move house");
        screen.ChosenPriority = screen.Priorities.Single(choice => choice.Value == "High");
        await screen.SaveListCommand.ExecutionTask!;

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Equal("High", screen.ChosenPriority.Value);
    }

    /// <summary>
    /// A private list this device cannot open: its readable fields are empty and its contents are in a
    /// payload no key here fits. Offering to edit it is worse than useless - saving would replace the
    /// sealed list with the empty one on screen.
    /// </summary>
    [Fact]
    public async Task A_private_list_this_device_cannot_open_is_read_only_here()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenPrivateTaskListAsync();

        Assert.True(screen.IsReadOnly);
        Assert.False(screen.CanEdit);
        Assert.NotEmpty(screen.ReadOnlyReason);
    }

    [Fact]
    public async Task Making_a_list_private_seals_it_and_leaves_the_readable_columns_empty()
    {
        using var context = new ScreenContext(PrivateContent.HoldingAKeyFor(Owner));
        var screen = context.OpenTaskList("Bank paperwork");

        screen.IsPrivate = true;
        await screen.SaveListCommand.ExecuteAsync(null);

        var stored = context.Stored();
        Assert.True(stored.IsPrivate);
        Assert.Equal(string.Empty, stored.Title);
        Assert.Empty(stored.Items);
        Assert.NotNull(stored.EncryptedContent);
    }

    [Fact]
    public async Task A_list_this_device_sealed_opens_again_with_its_entries_back()
    {
        using var context = new ScreenContext(PrivateContent.HoldingAKeyFor(Owner));
        var screen = context.OpenTaskList("Bank paperwork");
        screen.NewItemDescription = "call them";
        await screen.AddItemCommand.ExecuteAsync(null);

        screen.IsPrivate = true;
        await screen.SaveListCommand.ExecuteAsync(null);
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.False(screen.IsReadOnly);
        Assert.True(screen.IsPrivate);
        Assert.Equal("Bank paperwork", screen.Title);
        Assert.Equal(["call them"], screen.Items.Select(item => item.Description));
    }

    /// <summary>
    /// The server mints entry ids and never sees a private list's entries, so without one minted here
    /// every entry on the list would share the empty id - and ticking one would tick them all.
    /// </summary>
    [Fact]
    public async Task Every_entry_on_a_private_list_keeps_an_identity_of_its_own()
    {
        using var context = new ScreenContext(PrivateContent.HoldingAKeyFor(Owner));
        var screen = context.OpenTaskList("Bank paperwork");
        screen.NewItemDescription = "call them";
        await screen.AddItemCommand.ExecuteAsync(null);
        screen.NewItemDescription = "post the form";
        await screen.AddItemCommand.ExecuteAsync(null);

        screen.IsPrivate = true;
        await screen.SaveListCommand.ExecuteAsync(null);
        await screen.LoadCommand.ExecuteAsync(null);

        var ids = screen.Items.Select(item => item.Id).ToList();
        Assert.DoesNotContain(Guid.Empty, ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task A_private_list_is_not_offered_to_anybody()
    {
        using var context = new ScreenContext(PrivateContent.HoldingAKeyFor(Owner));
        var screen = context.OpenTaskList("Bank paperwork");

        screen.IsPrivate = true;
        await screen.SaveListCommand.ExecuteAsync(null);

        Assert.False(screen.Share.CanShare);
    }

    /// <inheritdoc cref="NoteDetailScreenTests"/>
    [Fact]
    public async Task Making_a_list_private_without_a_key_asks_for_it_rather_than_saving()
    {
        using var context = new ScreenContext(PrivateContent.SignedInWithoutAKey(Owner));
        var screen = context.OpenTaskList("Bank paperwork");

        screen.IsPrivate = true;
        await screen.SaveListCommand.ExecuteAsync(null);

        Assert.Contains(nameof(IScreenNavigator.ShowChatKeyGate), context.Navigator.Destinations);
        Assert.False(context.Stored().IsPrivate);
    }

    /// <summary>
    /// The wire carries a plain TimeOnly and cannot say "no hour", so an entry reminded daily at exactly
    /// midnight is read as one nobody chose an hour for - far likelier than one somebody wanted then,
    /// and being asked is a smaller cost than a reminder arriving while everybody is asleep.
    /// </summary>
    [Fact]
    public async Task A_daily_reminder_at_midnight_reads_as_one_with_no_hour_chosen()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Shopping");
        await context.AddRemindedAtMidnightAsync(screen, "Water the plants");

        await screen.LoadCommand.ExecuteAsync(null);
        screen.EditItemCommand.Execute(screen.Items.Single());

        Assert.False(screen.BeingEdited!.HasDailyReminderTime);
        Assert.False(screen.BeingEdited.CanSave);
        Assert.NotNull(screen.BeingEdited.WhatIsMissing);
    }

    [Fact]
    public async Task Choosing_an_hour_is_what_lets_it_be_saved_again()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Shopping");
        await context.AddRemindedAtMidnightAsync(screen, "Water the plants");
        await screen.LoadCommand.ExecuteAsync(null);
        screen.EditItemCommand.Execute(screen.Items.Single());

        screen.BeingEdited!.ChooseAReminderTimeCommand.Execute(null);

        Assert.True(screen.BeingEdited.HasDailyReminderTime);
        Assert.Equal(new TimeSpan(9, 0, 0), screen.BeingEdited.DailyReminderTime);
        Assert.True(screen.BeingEdited.CanSave);
        Assert.Null(screen.BeingEdited.WhatIsMissing);
    }

    /// <summary>An entry nobody asked to be reminded about is not missing an hour.</summary>
    [Fact]
    public async Task An_entry_with_no_daily_reminder_is_not_asked_for_an_hour()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Shopping");
        screen.NewItemDescription = "Buy flour";
        await screen.AddItemCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items.Single());

        Assert.True(screen.BeingEdited!.CanSave);
        Assert.Null(screen.BeingEdited.WhatIsMissing);
    }

    /// <summary>
    /// An errand about a shelf item opened and saved on the phone stays that errand. It used to come
    /// back a plain checklist entry, because the kind picker offered two of the three kinds and fell
    /// back to the first - and TaskItem drops LinkedInventoryItemId for any kind but Inventory, so the
    /// errand was cut loose from the product it was about, permanently and without a word.
    /// </summary>
    [Fact]
    public async Task An_errand_about_a_shelf_item_is_still_one_after_the_phone_saves_it()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Pantry");
        var product = await context.AddInventoryErrandAsync(screen, "Restock: Flour");
        await screen.LoadCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items.Single());
        await screen.SaveItemCommand.ExecuteAsync(null);

        var saved = context.Stored().Items.Single();
        Assert.Equal(nameof(TaskItemKind.Inventory), saved.Kind);
        Assert.Equal(product, saved.LinkedInventoryItemId);
    }

    [Fact]
    public async Task The_kind_of_an_errand_is_shown_for_what_it_is()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Pantry");
        await context.AddInventoryErrandAsync(screen, "Restock: Flour");
        await screen.LoadCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items.Single());

        Assert.Equal(nameof(TaskItemKind.Inventory), screen.BeingEdited!.ChosenKind!.Value);
    }

    /// <summary>Whoever is signed in - only its identity matters, as the key is kept per account.</summary>
    private static readonly Guid Owner = Guid.Parse("11111111-0000-4000-8000-000000000001");

    /// <summary>A phone with a local store and a server it can sometimes reach, and no MAUI in sight.</summary>
    private sealed class ScreenContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        /// <summary>The list OpenTaskList last made, so a helper can reach it behind the screen.</summary>
        private Guid _openedListId;
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-26T10:00:00Z"));
        private readonly LocalTaskListRepository _taskLists;

        private readonly PrivateContentSealer _privateContent;

        public ScreenContext(PrivateContentSealer? privateContent = null)
        {
            _privateContent = privateContent ?? PrivateContent.WithoutAKey();
            Server = new FakeTasksServer(_clock);
            CalendarServer = new FakeCalendarServer(_clock);
            _taskLists = new LocalTaskListRepository(_localStore, _clock, FixedNetworkStatus.Online, _privateContent);
            Shelves = new LocalWarehouseRepository(
                _localStore, _clock, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());
            ShelfSynchronizer = new WarehouseSynchronizer(
                _localStore, new InventoryClient(Warehouses.ToHttpClient()), _clock, new SyncGate(),
                NullLogger<WarehouseSynchronizer>.Instance);
            StockCheck = new StockCheckPanel(
                new TasksClient(Server.ToHttpClient()), new InventoryClient(Warehouses.ToHttpClient()),
                Shelves, new Translations(new InMemoryLanguageStore()));
            CalendarEvents = new LocalCalendarEventRepository(_localStore, _clock, FixedNetworkStatus.Online);
            Synchronizer = new TaskListSynchronizer(
                _localStore, new TasksClient(Server.ToHttpClient()), _clock, new SyncGate(),
                NullLogger<TaskListSynchronizer>.Instance);
        }

        public FakeTasksServer Server { get; }

        /// <summary>The map an entry's place can be pointed at on - see IPlacePicker.</summary>
        public FixedPlacePicker PlacePicker { get; } = new();

        /// <summary>This phone's copy of the calendar, which is where an entry's appointment is read from.</summary>
        public LocalCalendarEventRepository CalendarEvents { get; private set; } = null!;

        /// <summary>Where a Calendar entry's appointment is written - see PutInTheCalendarAsync.</summary>
        public FakeCalendarServer CalendarServer { get; }

        /// <summary>"Can this be done?" - see StockCheckPanel.</summary>
        public StockCheckPanel StockCheck { get; private set; } = null!;

        public TaskListSynchronizer Synchronizer { get; }

        public RecordingScreenNavigator Navigator { get; } = new();

        /// <summary>What this account has already named - see NameSuggestions. Empty unless a test fills it.</summary>
        public FakeSuggestionsServer SuggestionsServer { get; } = new();

        /// <summary>The shelves, which the stock check's refresh asks - see StockCheckPanel.</summary>
        public FakeInventoryServer Warehouses { get; } = new(TimeProvider.System);

        /// <summary>This phone's warehouses, which is where an errand's product is read from.</summary>
        public LocalWarehouseRepository Shelves { get; private set; } = null!;

        /// <summary>What carries a correction made here up to the server - see SaveTheShelfAsync.</summary>
        public WarehouseSynchronizer ShelfSynchronizer { get; private set; } = null!;

        /// <summary>
        /// An errand about one product on a shelf, as the restock machinery makes one. Written straight
        /// into the store: the phone has no way to link an entry to a shelf item itself.
        /// </summary>
        public async Task<Guid> AddInventoryErrandAsync(TaskListDetailViewModel screen, string description)
        {
            screen.NewItemDescription = description;
            await screen.AddItemCommand.ExecuteAsync(null);

            var product = Guid.NewGuid();
            await using var dbContext = _localStore.CreateDbContext();
            var stored = dbContext.TaskLists.Single();
            stored.Items = [.. stored.Items.Select(item => item with
            {
                Kind = nameof(TaskItemKind.Inventory),
                LinkedInventoryItemId = product
            })];

            await dbContext.SaveChangesAsync();
            return product;
        }

        /// <summary>
        /// An entry reminded daily at exactly midnight, which is what "nobody chose an hour" looks like
        /// on the wire. Written straight into the store: the phone's own editor cannot produce one.
        /// </summary>
        public async Task AddRemindedAtMidnightAsync(TaskListDetailViewModel screen, string description)
        {
            screen.NewItemDescription = description;
            await screen.AddItemCommand.ExecuteAsync(null);

            await using var dbContext = _localStore.CreateDbContext();
            var stored = dbContext.TaskLists.Single();
            stored.Items = [.. stored.Items.Select(item => item with
            {
                RemindDaily = true,
                DailyReminderTimeOfDay = default
            })];

            await dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// The one row as it really sits in the database, rather than as a read hands it back opened.
        /// One, because a context makes exactly one list per test.
        /// </summary>
        public LocalTaskList Stored()
        {
            using var dbContext = _localStore.CreateDbContext();
            return dbContext.TaskLists.Single();
        }

        /// <summary>A list sealed with a key this phone has not got, as the sync would bring one down.</summary>
        public async Task<TaskListDetailViewModel> OpenPrivateTaskListAsync()
        {
            var screen = OpenTaskList("Sealed");
            await using (var dbContext = _localStore.CreateDbContext())
            {
                dbContext.TaskLists.Single().IsPrivate = true;
                await dbContext.SaveChangesAsync();
            }

            await screen.LoadCommand.ExecuteAsync(null);
            return screen;
        }

        public TaskListDetailViewModel OpenTaskList(string title)
        {
            var created = _taskLists.CreateAsync(title, []).GetAwaiter().GetResult();
            var screen = new TaskListDetailViewModel(
                _taskLists, Synchronizer, new Translations(new InMemoryLanguageStore()), _clock,
                ShareTestPanel.For(_localStore, new ChatRepository(_localStore, _clock)), Navigator,
                new TasksClient(Server.ToHttpClient()), new CalendarClient(CalendarServer.ToHttpClient()),
                NothingIsBeingEdited(_clock), FixedNetworkStatus.Online,
                StockCheck, CalendarEvents, Shelves, ShelfSynchronizer,
                new InventoryClient(Warehouses.ToHttpClient()), PlacePicker, _privateContent,
                Suggestions.Offering(SuggestionsServer), Suggestions.Offering(SuggestionsServer));
            screen.Open(created.LocalId);
            screen.LoadCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            _openedListId = created.LocalId;
            return screen;
        }

        /// <summary>
        /// An appointment this phone already holds, as one an entry can carry the id of - which is what
        /// the entry's form is filled from when it is opened again.
        /// </summary>
        /// <summary>One product on one shelf, as this phone holds it.</summary>
        public async Task<(Guid WarehouseLocalId, Guid ProductId)> AddShelfProductAsync(
            string warehouseName, string productName, decimal quantity)
        {
            var warehouse = await Shelves.CreateAsync(warehouseName);
            var productId = Guid.NewGuid();
            await Shelves.UpdateAsync(
                warehouse.LocalId,
                new WarehouseContent(
                    warehouseName,
                    [new WarehouseItemDto(
                        productId, productName, "", "", quantity, null, nameof(InventoryUnit.Piece), null, "None")]));

            return (warehouse.LocalId, productId);
        }

        /// <summary>An entry standing for an errand about one product, saved as the wire has it.</summary>
        public async Task AddErrandAsync(TaskListDetailViewModel screen, string description, Guid productId)
        {
            screen.NewItemDescription = description;
            await screen.AddItemCommand.ExecuteAsync(null);

            var stored = await _taskLists.FindAsync(_openedListId);
            await _taskLists.UpdateAsync(
                _openedListId,
                new TaskListContent(
                    stored!.Title,
                    [.. stored.Items.Select(item => item with
                    {
                        Kind = nameof(TaskItemKind.Inventory),
                        LinkedInventoryItemId = productId
                    })],
                    stored.IsGroup, stored.Priority, stored.IsPrivate));

            await screen.LoadCommand.ExecuteAsync(null);
        }

        /// <summary>An entry already standing for an appointment the phone holds, saved as the wire has it.</summary>
        public async Task AddCalendarEntryAsync(TaskListDetailViewModel screen, string description, Guid eventId)
        {
            screen.NewItemDescription = description;
            await screen.AddItemCommand.ExecuteAsync(null);

            var stored = await _taskLists.FindAsync(_openedListId);
            await _taskLists.UpdateAsync(
                _openedListId,
                new TaskListContent(
                    stored!.Title,
                    [.. stored.Items.Select(item => item with
                    {
                        Kind = nameof(TaskItemKind.Calendar),
                        LinkedCalendarEventId = eventId
                    })],
                    stored.IsGroup, stored.Priority, stored.IsPrivate));

            await screen.LoadCommand.ExecuteAsync(null);
        }

        public async Task<Guid> AddCalendarEventAsync(
            string title, string? address, DateTimeOffset? startUtc = null, bool isAllDay = false)
        {
            var starts = startUtc ?? _clock.GetUtcNow();
            var created = await CalendarEvents.CreateAsync(new CalendarEventDetailsDto(
                title, "Bring the letter", address is null ? null : new EventLocationDto(address, 0, 0), null,
                starts, starts.AddHours(1), isAllDay, null, [], [30], "None", "Push"));

            await using var dbContext = _localStore.CreateDbContext();
            var stored = dbContext.CalendarEvents.Single(candidate => candidate.LocalId == created.LocalId);
            stored.ServerId = Guid.NewGuid();
            await dbContext.SaveChangesAsync();
            return stored.ServerId.Value;
        }

        public Task<SyncResult> SynchroniseAsync() => Synchronizer.SynchroniseAsync(CancellationToken.None);

        /// <summary>The list as the phone holds it, for the fields no screen property mirrors.</summary>
        public async Task<LocalTaskList> FindAsync(TaskListDetailViewModel screen)
        {
            await using var dbContext = _localStore.CreateDbContext();
            return dbContext.TaskLists.Single(list => list.Title == screen.Title);
        }

        /// <summary>A list with entries in the order they were typed, which is where arranging starts.</summary>
        public async Task<TaskListDetailViewModel> WithEntriesAsync(params string[] descriptions)
        {
            var screen = OpenTaskList("Morning");
            foreach (var description in descriptions)
            {
                screen.NewItemDescription = description;
                await screen.AddItemCommand.ExecuteAsync(null);
            }

            return screen;
        }

        /// <summary>
        /// A round of restocking as the warehouse's daily reminder leaves it: one errand, and the
        /// standing "Update stock levels" entry that closes the round - see RestockTaskNaming.
        /// </summary>
        public async Task<TaskListDetailViewModel> OpenRestockRoundAsync()
        {
            var screen = OpenTaskList("Restock");
            foreach (var description in new[] { "Buy flour", RestockTaskNaming.UpdateStockReminderDescription })
            {
                screen.NewItemDescription = description;
                await screen.AddItemCommand.ExecuteAsync(null);
            }

            return screen;
        }

        /// <summary>
        /// A warehouse's own restock list, as the server holds one: the title Orbit names it with, and a
        /// server id, which is what a settle needs to ask about anything. Opened a second time on
        /// purpose - the first open happens before the create has been accepted, which is the one moment
        /// a real list has no id either.
        /// </summary>
        public async Task<TaskListDetailViewModel> OpenManagedRestockListAsync()
        {
            var screen = OpenTaskList(RestockTaskNaming.TitleFor("Pantry"));
            screen.NewItemDescription = "Buy flour";
            await screen.AddItemCommand.ExecuteAsync(null);

            await screen.LoadCommand.ExecuteAsync(null);
            return screen;
        }

        public TaskItemRow StockReminderIn(TaskListDetailViewModel screen)
            => screen.Items.Single(row => row.Description == RestockTaskNaming.UpdateStockReminderDescription);

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
