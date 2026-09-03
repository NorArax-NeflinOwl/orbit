using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Inventories;
using Orbit.Core.Inventories;
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
    private static IEnumerable<string> Descriptions(TaskListDetailViewModel screen)
        => screen.Items.Select(item => item.Description);

    private static async Task AddAsync(TaskListDetailViewModel screen, params string[] descriptions)
    {
        foreach (var description in descriptions)
        {
            screen.NewItemDescription = description;
            await screen.AddItemCommand.ExecuteAsync(null);
        }
    }

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
    /// What an entry is about, typed as one line of words - the same box a shelf item's category is
    /// typed in, holding as many as apply. The tasks page finds an entry among every list by these.
    /// </summary>
    [Fact]
    public async Task An_entry_can_be_filed_under_what_it_is_about()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Errands");
        await AddAsync(screen, "Renew the car insurance");

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Categories = "car, money ,car";
        await screen.SaveItemCommand.ExecuteAsync(null);

        // Tidied on the way in, as the domain tidies it: trimmed, and the same word twice is one word.
        Assert.Equal(["car", "money"], Assert.Single(screen.Items).Item.AllCategories);

        // And sent, rather than left for the server to keep whatever it already had: a client that says
        // nothing about them cannot clear them.
        var sent = Assert.Single(Assert.Single(context.Server.TaskLists).Items);
        Assert.Equal(["car", "money"], sent.AllCategories);
    }

    /// <summary>Clearing the box clears them, which "not provided" would not do.</summary>
    [Fact]
    public async Task Clearing_what_it_is_filed_under_clears_it_everywhere()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Errands");
        await AddAsync(screen, "Renew the car insurance");
        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Categories = "car";
        await screen.SaveItemCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Categories = string.Empty;
        await screen.SaveItemCommand.ExecuteAsync(null);

        Assert.Empty(Assert.Single(screen.Items).Item.AllCategories);
        Assert.Empty(Assert.Single(Assert.Single(context.Server.TaskLists).Items).AllCategories);
    }

    /// <summary>
    /// The three orders Orbit.Web reads a checklist in. Alphabetical is by what each entry says, which
    /// is how a shopping list gets read off in a shop.
    /// </summary>
    [Fact]
    public async Task The_entries_are_read_in_the_chosen_order()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Groceries");
        await AddAsync(screen, "Milk", "Apples", "Bread");
        await screen.ToggleItemCommand.ExecuteAsync(screen.Items.Single(item => item.Description == "Apples"));

        Assert.Equal(["Milk", "Apples", "Bread"], Descriptions(screen));

        screen.ItemOrder = ChecklistOrder.Alphabetical;
        Assert.Equal(["Apples", "Bread", "Milk"], Descriptions(screen));

        // What is left to do first, then what is done, each alphabetically.
        screen.ItemOrder = ChecklistOrder.UndoneFirst;
        Assert.Equal(["Bread", "Milk", "Apples"], Descriptions(screen));

        screen.ItemOrder = ChecklistOrder.AsArranged;
        Assert.Equal(["Milk", "Apples", "Bread"], Descriptions(screen));
    }

    /// <summary>
    /// The order is how one person reads one list, so what is saved goes back arranged as it was - a
    /// sort that reached the save would rewrite everybody else's copy of the list.
    /// </summary>
    [Fact]
    public async Task Reading_it_in_another_order_does_not_rearrange_the_list()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Groceries");
        await AddAsync(screen, "Milk", "Apples");

        screen.ItemOrder = ChecklistOrder.Alphabetical;
        await screen.SaveListCommand.ExecuteAsync(null);

        Assert.Equal(
            ["Milk", "Apples"],
            context.Server.TaskLists.Single().Items.Select(item => item.Description));
    }

    /// <summary>
    /// Moving an entry is only offered in the arranged order: anywhere else "up" would move it in an
    /// arrangement nobody can see, and the entry would stay exactly where it is on screen.
    /// </summary>
    [Fact]
    public void Rearranging_is_offered_only_while_the_list_is_read_as_arranged()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Groceries");

        Assert.True(screen.CanBeRearranged);

        screen.ItemOrder = ChecklistOrder.Alphabetical;

        Assert.False(screen.CanBeRearranged);
    }

    [Fact]
    public async Task The_chosen_order_is_remembered_for_that_list()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Groceries");
        await AddAsync(screen, "Milk");

        screen.ItemOrder = ChecklistOrder.UndoneFirst;

        Assert.Equal(ChecklistOrder.UndoneFirst, context.Reading.Read(context.OpenedListId).Order);
    }

    /// <summary>
    /// Two parts of this screen write the one record. Each has to read it first, or folding the panel
    /// would put the entries back in the order nobody asked for - see ChecklistReading.
    /// </summary>
    [Fact]
    public void Folding_the_stock_check_leaves_the_chosen_order_alone()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Groceries");
        screen.ItemOrder = ChecklistOrder.Alphabetical;

        screen.StockCheck.ToggleFoldCommand.Execute(null);

        var reading = context.Reading.Read(context.OpenedListId);
        Assert.Equal(ChecklistOrder.Alphabetical, reading.Order);
        Assert.True(reading.IsStockCheckFolded);
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
    /// What the list is for, under its name - the field Orbit.Web gained on 2026-09-01 and the phone
    /// had nowhere to put. It is sent on every save rather than left unsaid: null means "not provided"
    /// on the way in, so a description cleared here would have come back at the next pull.
    /// </summary>
    [Fact]
    public async Task A_list_can_say_what_it_is_for()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Groceries");

        screen.Description = "The weekly shop, and nothing else.";
        // What leaving the box does, which is the only thing that saves it - an editor has no return key.
        await screen.CommitDescriptionCommand.ExecuteAsync(null);

        Assert.Equal("The weekly shop, and nothing else.", context.Server.TaskLists.Single().Description);

        screen.Description = string.Empty;
        await screen.CommitDescriptionCommand.ExecuteAsync(null);

        Assert.Empty(context.Server.TaskLists.Single().Description);
    }

    /// <summary>
    /// Leaving the box is what saves it, and only when something changed: a description is typed into an
    /// editor, which has no return key to press, and the first one written here was lost on the way out.
    /// </summary>
    [Fact]
    public async Task Leaving_the_description_alone_saves_nothing()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Groceries");
        var savesBefore = context.Server.ReceivedRequests.Count;

        await screen.CommitDescriptionCommand.ExecuteAsync(null);

        Assert.Equal(savesBefore, context.Server.ReceivedRequests.Count);
    }

    /// <summary>
    /// A private list keeps none: its title is sealed, and a description stored in the clear beside it
    /// would say in the open what the sealing is for. The server blanks it as well - this only agrees.
    /// </summary>
    [Fact]
    public async Task A_private_list_is_not_asked_what_it_is_for()
    {
        using var context = new ScreenContext(PrivateContent.HoldingAKeyFor(Owner));
        var screen = context.OpenTaskList("Doctor");

        screen.Description = "Prescriptions";
        screen.IsPrivate = true;
        // Waits on the save the switch started rather than starting a second: two saves race,
        // and the first can outlive the test - which brought the whole run down.
        await screen.SaveListCommand.ExecutionTask!;

        Assert.False(screen.IsNotPrivate);
        Assert.Empty(context.Server.TaskLists.Single().Description);
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
        await screen.ShowMapCommand.ExecuteAsync(null);
        await screen.SaveItemCommand.ExecuteAsync(null);

        var item = Assert.Single(screen.Items).Item;
        Assert.Equal(nameof(TaskItemKind.Calendar), item.Kind);
        // On the appointment, which is where it survives - see A_calendar_entrys_place_is_kept_on_its_appointment.
        Assert.Equal("12 Mill Lane", Assert.Single(context.CalendarServer.Events).Details.Location?.Address);
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
        var row = Assert.Single(screen.Items);
        Assert.Equal(appointment.Id, row.Item.LinkedCalendarEventId);
        Assert.True(row.HasReachedTheServer);
        Assert.False(row.IsWaitingToReachTheServer);
    }

    /// <summary>
    /// Where a calendar entry's place is actually kept, which this test used to get backwards.
    ///
    /// It asserted that the name stays on the entry and the appointment gets none - true of Orbit.Web,
    /// where an entry is tied to an event only if somebody picks one. On a phone saving a calendar entry
    /// always makes the appointment, so the entry is always tied, and an entry that is tied keeps no
    /// place at all (Orbit.Core's TaskItem.WhereItHappens clears it). The place was being thrown away on
    /// every save, and this test was watching it happen locally, before the round trip that dropped it.
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
        await screen.ShowMapCommand.ExecuteAsync(null);
        await screen.SaveItemCommand.ExecuteAsync(null);

        Assert.Equal("12 Mill Lane", Assert.Single(context.CalendarServer.Events).Details.Location?.Address);
    }

    /// <summary>
    /// The bug this stands for: with no route a request does not fail quickly, it hangs until the client
    /// gives up - which arrives as a timeout rather than an HttpRequestException. A save written to
    /// catch only the latter wrote the appointment nowhere and called the entry "online". Found on a
    /// device, which is the only place a phone actually has no route.
    /// </summary>
    [Fact]
    public async Task An_appointment_saved_where_the_connection_times_out_is_still_written_here()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Saturday");
        screen.NewItemDescription = "dentist";
        await screen.AddItemCommand.ExecuteAsync(null);
        context.CalendarServer.TimesOut = true;

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Kind = nameof(TaskItemKind.Calendar);
        screen.BeingEdited.Event.StartDate = new DateTime(2026, 9, 3);
        screen.BeingEdited.Event.EndDate = new DateTime(2026, 9, 3);
        await screen.SaveItemCommand.ExecuteAsync(null);

        Assert.Single(await context.CalendarEvents.GetAllAsync());
        Assert.True(Assert.Single(screen.Items).IsWaitingToReachTheServer);
    }

    /// <summary>
    /// An appointment can be made with no connection: it goes into this phone's own calendar and waits
    /// to be named. The entry carries no server id yet - which is what the row's "offline" tag says -
    /// and gets one when the calendar syncs.
    /// </summary>
    [Fact]
    public async Task An_appointment_made_with_no_connection_is_written_here_and_waits()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Saturday");
        screen.NewItemDescription = "dentist";
        await screen.AddItemCommand.ExecuteAsync(null);
        context.CalendarServer.IsUnreachable = true;

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Kind = nameof(TaskItemKind.Calendar);
        screen.BeingEdited.Event.StartDate = new DateTime(2026, 9, 3);
        screen.BeingEdited.Event.EndDate = new DateTime(2026, 9, 3);
        await screen.SaveItemCommand.ExecuteAsync(null);

        var stored = Assert.Single(await context.CalendarEvents.GetAllAsync());
        Assert.Equal("dentist", stored.Details.Title);
        Assert.Null(stored.ServerId);

        var row = Assert.Single(screen.Items);
        Assert.Null(row.Item.LinkedCalendarEventId);
        Assert.True(row.IsWaitingToReachTheServer);
        Assert.False(row.HasReachedTheServer);
    }

    /// <summary>
    /// Reopening one before it syncs shows what was typed. Without this the form would open empty and
    /// the next save would make a *second* appointment - the duplicate this whole pairing exists to stop.
    /// </summary>
    [Fact]
    public async Task An_appointment_still_waiting_reopens_on_what_was_typed()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Saturday");
        screen.NewItemDescription = "dentist";
        await screen.AddItemCommand.ExecuteAsync(null);
        context.CalendarServer.IsUnreachable = true;

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Kind = nameof(TaskItemKind.Calendar);
        screen.BeingEdited.Event.StartDate = new DateTime(2026, 9, 3);
        screen.BeingEdited.Event.EndDate = new DateTime(2026, 9, 3);
        screen.BeingEdited.Event.Description = "Bring the letter";
        await screen.SaveItemCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items[0]);
        Assert.Equal("Bring the letter", screen.BeingEdited!.Event.Description);
        Assert.Equal(new DateTime(2026, 9, 3), screen.BeingEdited.Event.StartDate);

        screen.BeingEdited.Event.Description = "Bring both letters";
        await screen.SaveItemCommand.ExecuteAsync(null);

        // One appointment, corrected - not two.
        var stored = Assert.Single(await context.CalendarEvents.GetAllAsync());
        Assert.Equal("Bring both letters", stored.Details.Description);
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
    /// means, so correcting an amount should not mean leaving the list and finding the inventory again.
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
    public async Task Correcting_a_product_from_an_errand_writes_it_back_to_the_inventory()
    {
        using var context = new ScreenContext();
        var shelf = await context.AddShelfProductAsync("Kitchen", "Coffee", quantity: 2);
        var screen = context.OpenTaskList("Saturday");
        await context.AddErrandAsync(screen, "Buy coffee", shelf.ProductId);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Shelf!.Product.Quantity = "7";
        screen.BeingEdited.Shelf.Product.Name = "Coffee, ground";
        await screen.SaveItemCommand.ExecuteAsync(null);

        var stored = await context.Shelves.FindAsync(shelf.InventoryLocalId);
        var product = Assert.Single(stored!.Items);
        Assert.Equal(7, product.Quantity);
        Assert.Equal("Coffee, ground", product.Name);
    }

    /// <summary>
    /// The correction has to leave the phone here, not whenever somebody next opens the inventory: this
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

        var onTheServer = Assert.Single(context.Inventories.Inventories);
        var product = Assert.Single(context.Inventories.ItemsIn(onTheServer.Id));
        Assert.Equal(7, product.Quantity);
    }

    /// <summary>
    /// The fields for a product appear when somebody says the entry is an errand, not after a save and
    /// a reopen. Found on a device: picking Inventory left the form saying "this entry isn't tied to a
    /// product yet", which is the message for a list with no shelf behind it - and the only way to the
    /// fields was to save, come back, and open it again.
    /// </summary>
    [Fact]
    public async Task Making_an_entry_an_errand_shows_the_product_it_would_describe()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Saturday");
        await context.MeasureAgainstAnEmptyShelfAsync(screen, "Kitchen");
        await AddAsync(screen, "Sugar");

        screen.EditItemCommand.Execute(screen.Items[0]);
        var editor = screen.BeingEdited!;
        Assert.False(editor.IsShelfEntry);

        editor.Kind = nameof(TaskItemKind.Inventory);

        Assert.True(editor.IsShelfEntry);
        Assert.True(editor.IsDescribingSomethingNew);
        Assert.Contains("Kitchen", editor.WhereTheProductLives);
    }

    /// <summary>And go again when the entry stops being one, rather than being saved to a shelf nobody asked.</summary>
    [Fact]
    public async Task An_entry_that_stops_being_an_errand_stops_describing_a_product()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Saturday");
        await context.MeasureAgainstAnEmptyShelfAsync(screen, "Kitchen");
        await AddAsync(screen, "Sugar");

        screen.EditItemCommand.Execute(screen.Items[0]);
        var editor = screen.BeingEdited!;
        editor.Kind = nameof(TaskItemKind.Inventory);
        editor.Kind = nameof(TaskItemKind.Checklist);

        Assert.False(editor.IsShelfEntry);
        Assert.Null(editor.Shelf);
    }

    /// <summary>
    /// A list measured against a shelf can say what it needs before that shelf holds it: the entry is
    /// the description, and saving puts the product there. Until now the phone could only correct
    /// something already on the shelf, so anything new had to be typed into the inventory first.
    /// </summary>
    [Fact]
    public async Task An_errand_for_something_not_on_the_shelf_yet_puts_it_there()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Saturday");
        var shelfLocalId = await context.MeasureAgainstAnEmptyShelfAsync(screen, "Kitchen");
        await context.AddErrandForSomethingNotOnTheShelfAsync(screen, "Coffee");

        screen.EditItemCommand.Execute(screen.Items[0]);
        var editor = screen.BeingEdited!;
        Assert.True(editor.IsDescribingSomethingNew);
        // The entry names it, so the form does not ask - see InventoryItemEditor.ShowsName.
        Assert.False(editor.Shelf!.Product.ShowsName);
        Assert.Contains("Kitchen", editor.WhereTheProductLives);

        editor.Shelf.Product.Quantity = "0";
        editor.Shelf.Product.MinimumQuantity = "2";
        await screen.SaveItemCommand.ExecuteAsync(null);

        var stored = await context.Shelves.FindAsync(shelfLocalId);
        var product = Assert.Single(stored!.Items);
        Assert.Equal("Coffee", product.Name);
        Assert.Equal(2, product.MinimumQuantity);
    }

    /// <summary>
    /// The stock check matches an errand to a product by name, so a second row of the same name would be
    /// two answers to "is there enough". The shelf already holding it is what the entry was asking for.
    /// Orbit.Web's own save skips it the same way.
    /// </summary>
    [Fact]
    public async Task An_errand_does_not_put_a_second_product_of_the_same_name_on_the_shelf()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Saturday");
        var shelfLocalId = await context.MeasureAgainstAnEmptyShelfAsync(screen, "Kitchen");
        await context.Shelves.UpdateAsync(
            shelfLocalId,
            new InventoryContent(
                "Kitchen",
                [new InventoryItemRequest(
                    Guid.NewGuid(), "coffee", "", "", 5, null, nameof(InventoryUnit.Piece), null, "None")]));
        await context.AddErrandForSomethingNotOnTheShelfAsync(screen, "Coffee");

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Shelf!.Product.Quantity = "0";
        await screen.SaveItemCommand.ExecuteAsync(null);

        var stored = await context.Shelves.FindAsync(shelfLocalId);
        var product = Assert.Single(stored!.Items);
        Assert.Equal(5, product.Quantity);
    }

    /// <summary>
    /// An inventory errand says which shelf it is about, and tapping that opens the inventory - the
    /// reason to show it at all is to be able to go and look. Orbit.Web asks its server for this; a
    /// phone works it out from what it already holds, so it still says so with no connection.
    /// </summary>
    [Fact]
    public async Task An_errand_says_which_shelf_it_is_about_and_opens_it()
    {
        using var context = new ScreenContext();
        var shelf = await context.AddShelfProductAsync("Kitchen", "Coffee", quantity: 2);
        var screen = context.OpenTaskList("Saturday");
        await context.AddErrandAsync(screen, "Buy coffee", shelf.ProductId);

        var reference = Assert.Single(Assert.Single(screen.Items).References);
        Assert.Contains("Kitchen", reference.Label);

        screen.OpenReferenceCommand.Execute(reference);

        Assert.Equal(shelf.InventoryLocalId, context.Navigator.LastInventoryId);
        // And on the product itself: a shelf of sixty rows with no sign of which one the errand meant
        // sends somebody looking for it a second time.
        Assert.Equal(shelf.ProductId, context.Navigator.LastPointedAtProductId);
    }

    /// <summary>
    /// When a second list is asking for the same product, the errand says where else - so somebody about
    /// to buy coffee knows the other list wants it too, instead of buying twice.
    /// </summary>
    [Fact]
    public async Task An_errand_says_which_other_list_is_asking_for_the_same_product()
    {
        using var context = new ScreenContext();
        var shelf = await context.AddShelfProductAsync("Kitchen", "Coffee", quantity: 2);
        var otherList = context.OpenTaskList("Weekend");
        await context.AddErrandAsync(otherList, "Buy coffee", shelf.ProductId);

        var screen = context.OpenTaskList("Saturday");
        await context.AddErrandAsync(screen, "Buy coffee", shelf.ProductId);

        var elsewhere = Assert.Single(
            Assert.Single(screen.Items).References,
            reference => reference.Target == TaskItemReferenceTarget.TaskList);
        Assert.Contains("Weekend", elsewhere.Label);

        screen.OpenReferenceCommand.Execute(elsewhere);
        Assert.Equal(elsewhere.LocalId, context.Navigator.LastTaskListId);
    }

    /// <summary>A plain errand points nowhere, and says nothing - which is most entries.</summary>
    [Fact]
    public async Task An_ordinary_entry_has_nothing_to_point_at()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Saturday");
        screen.NewItemDescription = "Buy milk";
        await screen.AddItemCommand.ExecuteAsync(null);

        Assert.Empty(Assert.Single(screen.Items).References);
    }

    /// <summary>
    /// An errand whose product this phone has not got - an inventory no longer shared, or one not synced
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
    /// <summary>
    /// The place somebody typed was being thrown away on every save, and this is where it went: an entry
    /// tied to an appointment keeps no place of its own (see Orbit.Core's TaskItem.WhereItHappens), and
    /// on a phone saving a calendar entry always makes the appointment - so the entry was always tied,
    /// and the box always emptied itself. The place belongs on the appointment.
    /// </summary>
    [Fact]
    public async Task A_calendar_entrys_place_is_kept_on_its_appointment()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Saturday");
        screen.NewItemDescription = "dentist";
        await screen.AddItemCommand.ExecuteAsync(null);
        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Kind = nameof(TaskItemKind.Calendar);

        await screen.ShowMapCommand.ExecuteAsync(null);
        await screen.SaveItemCommand.ExecuteAsync(null);

        var appointment = Assert.Single(context.CalendarServer.Events);
        Assert.Equal("12 Mill Lane", appointment.Details.Location?.Address);
        Assert.Equal(52.23, appointment.Details.Location?.Latitude);
    }

    /// <summary>And it comes back on the next open, from where it was actually stored.</summary>
    [Fact]
    public async Task That_place_is_there_again_when_the_entry_is_reopened()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Saturday");
        screen.NewItemDescription = "dentist";
        await screen.AddItemCommand.ExecuteAsync(null);
        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Kind = nameof(TaskItemKind.Calendar);
        await screen.ShowMapCommand.ExecuteAsync(null);
        await screen.SaveItemCommand.ExecuteAsync(null);

        await context.SynchroniseCalendarAsync();
        await screen.LoadCommand.ExecuteAsync(null);
        screen.EditItemCommand.Execute(screen.Items[0]);

        Assert.Equal("12 Mill Lane", screen.BeingEdited!.Location);
    }

    /// <summary>
    /// A name nothing could be found for cannot be stored - an appointment holds a point first - so the
    /// screen says so rather than saving one that quietly lost it.
    /// </summary>
    [Fact]
    public async Task A_place_that_cannot_be_found_is_reported_rather_than_dropped_in_silence()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Saturday");
        screen.NewItemDescription = "dentist";
        await screen.AddItemCommand.ExecuteAsync(null);
        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Kind = nameof(TaskItemKind.Calendar);
        screen.BeingEdited.Location = "somewhere nobody can find";

        await screen.SaveItemCommand.ExecuteAsync(null);

        Assert.True(screen.HasStatus);
    }

    /// <summary>
    /// The map opens where the box already points, so a reader who has half an address does not start
    /// in the middle of the ocean. What it answers with lands on the entry as a position; the words in
    /// the box are the reader's - see A_confirmed_pin_leaves_a_name_the_reader_wrote_alone.
    /// </summary>
    [Fact]
    public async Task A_place_can_be_pointed_at_on_the_map()
    {
        using var context = new ScreenContext();
        context.PlacePicker.Result = PickedPlace.Chosen("12 Mill Lane", 52.23, 21.01);
        var screen = context.OpenTaskList("Saturday");
        screen.NewItemDescription = "dentist";
        await screen.AddItemCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Kind = nameof(TaskItemKind.Calendar);
        screen.BeingEdited.Location = "Mill Lane";
        await screen.ShowMapCommand.ExecuteAsync(null);

        Assert.Equal("Mill Lane", context.PlacePicker.StartedAt);
        Assert.Equal(52.23, screen.BeingEdited.LocationLatitude);
        Assert.Equal(21.01, screen.BeingEdited.LocationLongitude);
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
    /// Confirming a pin keeps a name the reader wrote. "The back entrance" is the whole reason that box
    /// is typed into, and this screen replaced it with the street on every confirmed pin - so correcting
    /// it was impossible for as long as the map was the way to set the place. Orbit.Web draws the line
    /// in the same spot, and so does this phone's own calendar screen; only the entry editor did not.
    ///
    /// The old tests could not see it: the map answered with the same address the test had typed.
    /// </summary>
    [Fact]
    public async Task A_confirmed_pin_leaves_a_name_the_reader_wrote_alone()
    {
        using var context = new ScreenContext();
        context.PlacePicker.Result = PickedPlace.Chosen("12 Mill Lane", 52.23, 21.01);
        var screen = context.OpenTaskList("Saturday");
        screen.NewItemDescription = "dentist";
        await screen.AddItemCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Kind = nameof(TaskItemKind.Calendar);
        screen.BeingEdited.Location = "the back entrance";
        await screen.ShowMapCommand.ExecuteAsync(null);

        Assert.Equal("the back entrance", screen.BeingEdited.Location);
        // Where it is was decided by the pin either way - that is what the appointment stores.
        Assert.Equal(52.23, screen.BeingEdited.LocationLatitude);
        Assert.Equal(21.01, screen.BeingEdited.LocationLongitude);
    }

    /// <summary>And it fills a box nobody has written in, which is the point of opening the map.</summary>
    [Fact]
    public async Task A_confirmed_pin_names_a_place_that_had_no_name()
    {
        using var context = new ScreenContext();
        context.PlacePicker.Result = PickedPlace.Chosen("12 Mill Lane", 52.23, 21.01);
        var screen = context.OpenTaskList("Saturday");
        screen.NewItemDescription = "dentist";
        await screen.AddItemCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Kind = nameof(TaskItemKind.Calendar);
        await screen.ShowMapCommand.ExecuteAsync(null);

        Assert.Equal("12 Mill Lane", screen.BeingEdited.Location);
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
        // Choosing from the picker adds a list rather than replacing what is there, and the picker
        // clears itself: it says what to add next, not what the entry already stands for.
        screen.BeingEdited!.LinkToCommand.Execute(
            screen.BeingEdited.LinkableTaskLists.Single(choice => choice.Name == "Shopping"));
        // The picker goes on offering what to add next: what the entry already stands for is off it.
        Assert.DoesNotContain(
            screen.BeingEdited.LinkableTaskListsLeft, choice => choice.Name == "Shopping");
        await screen.SaveItemCommand.ExecuteAsync(null);
        await context.SynchroniseAsync();

        var shopping = context.Server.TaskLists.Single(list => list.Title == "Shopping");
        var thisWeek = context.Server.TaskLists.Single(list => list.Title == "This week");
        Assert.Equal([shopping.Id], Assert.Single(thisWeek.Items).AllLinkedTaskListIds);
    }

    /// <summary>
    /// The bug this stands for: moving an entry onto a list it already stands for took the whole app
    /// down. The server refuses it - an entry cannot link to the list it belongs to - as a 400 with a
    /// message, and the phone turned every unexpected status into an exception nothing caught.
    /// </summary>
    [Fact]
    public async Task A_move_the_server_refuses_is_said_rather_than_thrown()
    {
        using var context = new ScreenContext();
        var later = context.OpenTaskList("Later");
        var screen = context.OpenTaskList("Today");
        await AddAsync(screen, "Call the plumber");
        await context.SynchroniseAsync();
        await screen.LoadCommand.ExecuteAsync(null);
        screen.EditItemCommand.Execute(screen.Items.Single());
        context.Server.RefusesTheNextMove = true;

        await screen.MoveItemCommand.ExecuteAsync(
            screen.MoveTargets.Single(target => target.Name == "Later"));

        // Said, and said as something that will not come right by trying again.
        Assert.Contains("isn't allowed", screen.Status);
        Assert.Single(screen.Items);
    }

    /// <summary>
    /// An entry cannot link to the list it belongs to, so a list it already stands for is not somewhere
    /// it can be moved - the server refuses that move outright. Left out of the picker rather than
    /// offered and then rejected, which is what Orbit.Web's editor does too.
    /// </summary>
    [Fact]
    public async Task A_list_the_entry_stands_for_is_not_offered_as_somewhere_to_move_it()
    {
        using var context = new ScreenContext();
        context.OpenTaskList("Shopping");
        context.OpenTaskList("Later");
        var screen = context.OpenTaskList("Today");
        await AddAsync(screen, "The shopping");
        await context.SynchroniseAsync();
        await screen.LoadCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items.Single());
        Assert.Equal(
            ["Later", "Shopping"],
            screen.MoveTargetsForTheEntry.Select(target => target.Name).Order());

        screen.BeingEdited!.LinkToCommand.Execute(
            screen.BeingEdited.LinkableTaskLists.Single(choice => choice.Name == "Shopping"));

        Assert.Equal(["Later"], screen.MoveTargetsForTheEntry.Select(target => target.Name));
    }

    /// <summary>
    /// A group list is nothing but entries standing for other lists, and this screen offered no way
    /// into any of them: the work it gathers was one tap away in the browser and unreachable here.
    /// The browser stacks the whole tree as cards; a phone has room for one list at a time, so the
    /// entry says which lists it stands for and each opens.
    /// </summary>
    [Fact]
    public async Task An_entry_standing_for_a_list_is_the_way_into_it()
    {
        using var context = new ScreenContext();
        var shopping = context.OpenTaskList("Shopping");
        var screen = context.OpenTaskList("This week");
        await AddAsync(screen, "The shopping");
        await context.SynchroniseAsync();
        await screen.LoadCommand.ExecuteAsync(null);
        screen.EditItemCommand.Execute(screen.Items.Single());
        screen.BeingEdited!.LinkToCommand.Execute(
            screen.BeingEdited.LinkableTaskLists.Single(choice => choice.Name == "Shopping"));
        await screen.SaveItemCommand.ExecuteAsync(null);
        await context.SynchroniseAsync();
        await screen.LoadCommand.ExecuteAsync(null);

        var reference = Assert.Single(Assert.Single(screen.Items).References);
        Assert.Equal("Shopping", reference.Label);

        screen.OpenReferenceCommand.Execute(reference);

        Assert.Equal("ShowTaskList", context.Navigator.LastDestination);
        Assert.Equal(context.OpenedListIdOf("Shopping"), context.Navigator.LastTaskListId);
    }

    /// <summary>A list this phone has not got is no way in at all, so no chip is offered for it.</summary>
    [Fact]
    public async Task A_list_this_phone_has_not_got_offers_nothing_to_open()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("This week");
        await AddAsync(screen, "The shopping");
        await context.SynchroniseAsync();
        await screen.LoadCommand.ExecuteAsync(null);

        // An entry pointing at a list nobody here holds - shared and then unshared, or not pulled yet.
        await context.PointAtAListNobodyHoldsAsync(screen);
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Empty(Assert.Single(screen.Items).References);
    }

    /// <summary>
    /// One entry, several lists - what the web gained on 2026-09-01. The phone carried them from the
    /// first sync but showed one and would have sent one back, so the second list was lost to whichever
    /// phone touched the entry next.
    /// </summary>
    [Fact]
    public async Task An_entry_can_stand_for_several_lists()
    {
        using var context = new ScreenContext();
        context.OpenTaskList("Shopping");
        context.OpenTaskList("Chemist");
        var screen = context.OpenTaskList("This week");
        screen.NewItemDescription = "The errands";
        await screen.AddItemCommand.ExecuteAsync(null);
        await context.SynchroniseAsync();
        await screen.LoadCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items.Single());
        var editor = screen.BeingEdited!;
        editor.LinkToCommand.Execute(editor.LinkableTaskLists.Single(choice => choice.Name == "Shopping"));
        editor.LinkToCommand.Execute(editor.LinkableTaskLists.Single(choice => choice.Name == "Chemist"));

        Assert.Equal(["Shopping", "Chemist"], editor.LinkedTaskLists.Select(linked => linked.Name));
        // And what it already stands for is not offered again.
        Assert.DoesNotContain(editor.LinkableTaskListsLeft, choice => choice.Name == "Shopping");

        await screen.SaveItemCommand.ExecuteAsync(null);
        await context.SynchroniseAsync();

        var thisWeek = context.Server.TaskLists.Single(list => list.Title == "This week");
        Assert.Equal(2, Assert.Single(thisWeek.Items).AllLinkedTaskListIds.Count);

        // And they are still both there when the entry is opened again.
        await screen.LoadCommand.ExecuteAsync(null);
        screen.EditItemCommand.Execute(screen.Items.Single());
        Assert.Equal(["Shopping", "Chemist"], screen.BeingEdited!.LinkedTaskLists.Select(linked => linked.Name));
    }

    /// <summary>
    /// An entry standing for another list is done when that list is, so its box cannot be ticked here.
    /// The press is taken rather than refused: it names the list and offers to go there, which is the
    /// question Orbit.Web asks under the same row. Pressed and silently ignored, the phone looked broken.
    /// </summary>
    [Fact]
    public async Task Ticking_an_entry_that_stands_for_a_list_asks_about_that_list_instead()
    {
        using var context = new ScreenContext();
        context.OpenTaskList("Shopping");
        var screen = context.OpenTaskList("This week");
        screen.NewItemDescription = "The shopping";
        await screen.AddItemCommand.ExecuteAsync(null);
        await context.SynchroniseAsync();
        await screen.LoadCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items.Single());
        screen.BeingEdited!.LinkToCommand.Execute(
            screen.BeingEdited.LinkableTaskLists.Single(choice => choice.Name == "Shopping"));
        await screen.SaveItemCommand.ExecuteAsync(null);

        await screen.ToggleItemCommand.ExecuteAsync(screen.Items.Single());

        Assert.True(screen.IsAskingAboutTheListsBehind);
        Assert.Contains("Shopping", screen.ListsBehindTheEntryQuestion);
        // Nothing was ticked: the answer is on the other list.
        Assert.False(screen.Items.Single().IsCompleted);

        var shopping = Assert.Single(screen.ListsBehindTheEntry);
        Assert.Equal("Shopping", shopping.Label);
        screen.OpenTheListBehindCommand.Execute(shopping);

        Assert.False(screen.IsAskingAboutTheListsBehind);
        Assert.Equal(shopping.LocalId, context.Navigator.LastTaskListId);
    }

    /// <summary>"No" leaves both the entry and the question where they were.</summary>
    [Fact]
    public async Task The_question_can_be_left_alone()
    {
        using var context = new ScreenContext();
        context.OpenTaskList("Shopping");
        var screen = context.OpenTaskList("This week");
        screen.NewItemDescription = "The shopping";
        await screen.AddItemCommand.ExecuteAsync(null);
        await context.SynchroniseAsync();
        await screen.LoadCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items.Single());
        screen.BeingEdited!.LinkToCommand.Execute(
            screen.BeingEdited.LinkableTaskLists.Single(choice => choice.Name == "Shopping"));
        await screen.SaveItemCommand.ExecuteAsync(null);
        await screen.ToggleItemCommand.ExecuteAsync(screen.Items.Single());

        screen.LeaveTheListBehindCommand.Execute(null);

        Assert.False(screen.IsAskingAboutTheListsBehind);
        Assert.False(screen.Items.Single().IsCompleted);
        Assert.Null(context.Navigator.LastTaskListId);
    }

    /// <summary>
    /// And can stop standing for it - one list at a time, since it may stand for several. Taking the
    /// last one off leaves an ordinary entry, which is what most of them are.
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
        screen.BeingEdited!.LinkToCommand.Execute(
            screen.BeingEdited.LinkableTaskLists.Single(choice => choice.Name == "Shopping"));
        await screen.SaveItemCommand.ExecuteAsync(null);

        await screen.LoadCommand.ExecuteAsync(null);
        screen.EditItemCommand.Execute(screen.Items.Single());
        var linked = Assert.Single(screen.BeingEdited!.LinkedTaskLists);
        Assert.Equal("Shopping", linked.Name);

        screen.BeingEdited.UnlinkCommand.Execute(linked);
        Assert.False(screen.BeingEdited.IsALinkToOtherLists);
        await screen.SaveItemCommand.ExecuteAsync(null);
        await context.SynchroniseAsync();

        var thisWeek = context.Server.TaskLists.Single(list => list.Title == "This week");
        Assert.Empty(Assert.Single(thisWeek.Items).AllLinkedTaskListIds);
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
    /// Opening an inventory's restock list settles what is already crossed off on it - each finished
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
    /// Nothing to settle on a list no inventory tracks, so nothing is asked - the title is the only way
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
    public async Task Answering_yes_brings_the_whole_inventory_up_to_its_minimum()
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
    /// inventory was declined.
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
        // Waits on the save the switch started rather than starting a second: two saves race,
        // and the first can outlive the test - which brought the whole run down.
        await screen.SaveListCommand.ExecutionTask!;

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
        // Waits on the save the switch started rather than starting a second: two saves race,
        // and the first can outlive the test - which brought the whole run down.
        await screen.SaveListCommand.ExecutionTask!;
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
        // Waits on the save the switch started rather than starting a second: two saves race,
        // and the first can outlive the test - which brought the whole run down.
        await screen.SaveListCommand.ExecutionTask!;
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
        // Waits on the save the switch started rather than starting a second: two saves race,
        // and the first can outlive the test - which brought the whole run down.
        await screen.SaveListCommand.ExecutionTask!;

        Assert.False(screen.Share.CanShare);
    }

    /// <inheritdoc cref="NoteDetailScreenTests"/>
    [Fact]
    public async Task Making_a_list_private_without_a_key_asks_for_it_rather_than_saving()
    {
        using var context = new ScreenContext(PrivateContent.SignedInWithoutAKey(Owner));
        var screen = context.OpenTaskList("Bank paperwork");

        screen.IsPrivate = true;
        // Waits on the save the switch started rather than starting a second: two saves race,
        // and the first can outlive the test - which brought the whole run down.
        await screen.SaveListCommand.ExecutionTask!;

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

        /// <summary>Which list is open, for a test that asks what was remembered about it.</summary>
        public Guid OpenedListId => _openedListId;

        /// <summary>This phone's id for a list a test opened earlier, for asserting where a tap led.</summary>
        public Guid OpenedListIdOf(string title)
            => _taskLists.GetAllAsync().GetAwaiter().GetResult().Single(list => list.Title == title).LocalId;

        /// <summary>
        /// Makes the open list's entry stand for a list this phone does not hold - what a share taken
        /// back leaves behind.
        /// </summary>
        public async Task PointAtAListNobodyHoldsAsync(TaskListDetailViewModel screen)
        {
            var stored = await _taskLists.FindAsync(_openedListId);
            await _taskLists.UpdateAsync(
                _openedListId,
                new TaskListContent(
                    stored!.Title,
                    [.. stored.Items.Select(item => item with { LinkedTaskListIds = [Guid.NewGuid()] })],
                    stored.IsGroup,
                    stored.Priority));
        }
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-26T10:00:00Z"));
        private readonly LocalTaskListRepository _taskLists;

        private readonly PrivateContentSealer _privateContent;

        public ScreenContext(PrivateContentSealer? privateContent = null)
        {
            _privateContent = privateContent ?? PrivateContent.WithoutAKey();
            Server = new FakeTasksServer(_clock);
            CalendarServer = new FakeCalendarServer(_clock);
            _taskLists = new LocalTaskListRepository(_localStore, _clock, FixedNetworkStatus.Online, _privateContent);
            Shelves = new LocalInventoryRepository(
                _localStore, _clock, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());
            ShelfSynchronizer = new InventorySynchronizer(
                _localStore, new InventoryClient(Inventories.ToHttpClient()), _clock, new SyncGate(),
                NullLogger<InventorySynchronizer>.Instance);
            StockCheck = new StockCheckPanel(
                new TasksClient(Server.ToHttpClient()), new InventoryClient(Inventories.ToHttpClient()),
                Shelves, new Translations(new InMemoryLanguageStore()), Connections.Online, Reading);
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

        /// <summary>
        /// Brings this phone's calendar into step with the fake server's, so an entry saved a moment ago
        /// reopens knowing the appointment it made - which is where its place is now kept.
        /// </summary>
        public Task SynchroniseCalendarAsync()
            => new CalendarEventSynchronizer(
                _localStore, new CalendarClient(CalendarServer.ToHttpClient()), _clock, new SyncGate(),
                new PendingCalendarLinkResolver(_clock, NullLogger<PendingCalendarLinkResolver>.Instance),
                NullLogger<CalendarEventSynchronizer>.Instance).SynchroniseAsync(CancellationToken.None);

        /// <summary>Where a Calendar entry's appointment is written - see PutInTheCalendarAsync.</summary>
        public FakeCalendarServer CalendarServer { get; }

        /// <summary>"Can this be done?" - see StockCheckPanel.</summary>
        public StockCheckPanel StockCheck { get; private set; } = null!;

        public TaskListSynchronizer Synchronizer { get; }

        public RecordingScreenNavigator Navigator { get; } = new();

        /// <summary>What this account has already named - see NameSuggestions. Empty unless a test fills it.</summary>
        public FakeSuggestionsServer SuggestionsServer { get; } = new();

        /// <summary>The shelves, which the stock check's refresh asks - see StockCheckPanel.</summary>
        public FakeInventoryServer Inventories { get; } = new(TimeProvider.System);

        /// <summary>This phone's inventories, which is where an errand's product is read from.</summary>
        public LocalInventoryRepository Shelves { get; private set; } = null!;

        /// <summary>What carries a correction made here up to the server - see SaveTheShelfAsync.</summary>
        public InventorySynchronizer ShelfSynchronizer { get; private set; } = null!;

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

        /// <summary>Whether the phone has a connection, shared by the screen and what it saves through.</summary>
        public FixedNetworkStatus Network { get; } = FixedNetworkStatus.Online;

        /// <summary>
        /// Where a typed place turns out to be. Unreachable by default, which is the honest stand-in for
        /// a third-party lookup no test should depend on - a place typed without a pin is then one
        /// nothing could be found for, which is the case worth checking anyway.
        /// </summary>
        public PlaceSearch Places { get; } = new(StubHttpMessageHandler.Unreachable().ToHttpClient());

        /// <summary>
        /// How this reader reads this list - shared with the stock check panel, as it is on the device,
        /// so a test can check the two do not write over each other.
        /// </summary>
        public InMemoryChecklistReadingStore Reading { get; } = new();

        public TaskListDetailViewModel OpenTaskList(string title)
        {
            var created = _taskLists.CreateAsync(title, []).GetAwaiter().GetResult();
            var screen = new TaskListDetailViewModel(
                _taskLists, Synchronizer, new Translations(new InMemoryLanguageStore()), _clock,
                ShareTestPanel.For(_localStore, new ChatRepository(_localStore, _clock)), Navigator,
                new TasksClient(Server.ToHttpClient()),
                NothingIsBeingEdited(_clock), Network,
                StockCheck,
                new EntryAppointment(
                    CalendarEvents, new CalendarClient(CalendarServer.ToHttpClient()), Network, Places),
                new ShelfCorrection(Shelves, ShelfSynchronizer, new InventoryClient(Inventories.ToHttpClient())),
                PlacePicker, _privateContent,
                Suggestions.Offering(SuggestionsServer), Suggestions.Offering(SuggestionsServer), Reading);
            screen.Open(created.LocalId);
            screen.LoadCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            _openedListId = created.LocalId;
            return screen;
        }

        /// <summary>
        /// An appointment this phone already holds, as one an entry can carry the id of - which is what
        /// the entry's form is filled from when it is opened again.
        /// </summary>
        /// <summary>
        /// An empty shelf this list is measured against, as the stock check leaves things: the inventory
        /// is pushed up so it has a server id, and the list is pointed at that id the way a pull would
        /// point it. Written to the store directly because the link is made on the server and comes back
        /// down - there is no local write for it.
        /// </summary>
        public async Task<Guid> MeasureAgainstAnEmptyShelfAsync(TaskListDetailViewModel screen, string inventoryName)
        {
            var inventory = await Shelves.CreateAsync(inventoryName);
            await ShelfSynchronizer.SynchroniseAsync();
            await Synchronizer.SynchroniseAsync(CancellationToken.None);

            Guid inventoryServerId;
            await using (var dbContext = _localStore.CreateDbContext())
            {
                inventoryServerId = dbContext.Inventories
                    .Single(candidate => candidate.LocalId == inventory.LocalId).ServerId!.Value;
            }

            // Through the server rather than into the row, because that is where the link lives - a
            // phone learns which shelf a list is measured against by pulling the list back down.
            await new TasksClient(Server.ToHttpClient()).LinkInventoryAsync(
                Stored().ServerId!.Value, inventoryServerId);
            await screen.LoadCommand.ExecuteAsync(null);
            return inventory.LocalId;
        }

        /// <summary>An entry standing for an errand about nothing on the shelf yet.</summary>
        public async Task AddErrandForSomethingNotOnTheShelfAsync(
            TaskListDetailViewModel screen, string description)
        {
            screen.NewItemDescription = description;
            await screen.AddItemCommand.ExecuteAsync(null);

            var stored = await _taskLists.FindAsync(_openedListId);
            await _taskLists.UpdateAsync(
                _openedListId,
                new TaskListContent(
                    stored!.Title,
                    [.. stored.Items.Select(item => item with { Kind = nameof(TaskItemKind.Inventory) })],
                    stored.IsGroup, stored.Priority, stored.IsPrivate));

            await screen.LoadCommand.ExecuteAsync(null);
        }

        /// <summary>One product on one shelf, as this phone holds it.</summary>
        public async Task<(Guid InventoryLocalId, Guid ProductId)> AddShelfProductAsync(
            string inventoryName, string productName, decimal quantity)
        {
            var inventory = await Shelves.CreateAsync(inventoryName);
            var productId = Guid.NewGuid();
            await Shelves.UpdateAsync(
                inventory.LocalId,
                new InventoryContent(
                    inventoryName,
                    [new InventoryItemRequest(
                        productId, productName, "", "", quantity, null, nameof(InventoryUnit.Piece), null, "None")]));

            return (inventory.LocalId, productId);
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
        /// A round of restocking as the inventory's daily reminder leaves it: one errand, and the
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
        /// An inventory's own restock list, as the server holds one: the title Orbit names it with, and a
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
